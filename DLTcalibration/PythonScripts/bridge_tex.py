import torch
import numpy as np
import yaml
import os
import sys
import argparse
import glob

# ==============================================================================
# ★ [설정] 기존 파일들의 위치를 정확히 지정해주세요 (WSL 절대경로 권장)
# Unity 프로젝트 폴더 내에 있다면, 상대 경로로 맞춰도 됩니다.
# ==============================================================================
BASE_DIR = os.path.dirname(os.path.abspath(__file__)) # 현재 스크립트 위치

# 모델 체크포인트 경로 (기존 코드의 MODEL_PATH)
CKPT_PATH = os.path.join(BASE_DIR, 'ckpt_root/MeshNet_best.pkl') 

# 설정 파일 경로
CONFIG_PATH = os.path.join(BASE_DIR, 'config/MeshSaliency.yaml')

# 필요한 라이브러리 경로 추가 (혹시 모듈 에러 나면 주석 해제 후 경로 수정)
# sys.path.append(os.path.join(BASE_DIR, '..')) 
# ==============================================================================

from models import MeshTextureNet
from data.MeshRotateDataset import MeshDataset

def get_original_vertex_count(mesh_path):
    """
    원본 정점 개수 카운트 (Unity가 보낸 파일 직접 읽기)
    """
    if not os.path.exists(mesh_path): return None
    v_count = 0
    try:
        with open(mesh_path, 'r', errors='ignore') as f:
            for line in f:
                if line.startswith('v '): v_count += 1
        return v_count
    except: return None

def get_saliency_for_single_mesh(model, device, collated_dict):
    # (기존 코드와 동일)
    verts = collated_dict['verts'].to(device).float()
    faces = collated_dict['faces'].to(device).long()
    
    centers = collated_dict['centers'].to(device).float().permute(0, 2, 1)
    normals = collated_dict['normals'].to(device).float().permute(0, 2, 1)
    corners = collated_dict['corners'].to(device).float().permute(0, 2, 1)
    
    neighbor_index = collated_dict['neighbors'].to(device).long()
    ring_1 = collated_dict['ring_1'].to(device).long()
    ring_2 = collated_dict['ring_2'].to(device).long()
    ring_3 = collated_dict['ring_3'].to(device).long()
    
    texture = collated_dict['texture'].to(device).float()
    uv_grid = collated_dict['uv_grid'].to(device).float()

    saliency_scores = model(verts=verts, faces=faces, centers=centers, normals=normals,
                            corners=corners, neighbor_index=neighbor_index, ring_1=ring_1,
                            ring_2=ring_2, ring_3=ring_3, face_colors=0, face_textures=0,
                            texture=texture, uv_grid=uv_grid)
    
    return saliency_scores.detach().cpu().numpy().flatten()

def convert_face_to_vertex_saliency(faces, face_scores, num_vertices):
    # (기존 코드와 동일)
    vertex_scores_sum = np.zeros(num_vertices)
    vertex_face_counts = np.zeros(num_vertices)
    for face_index, vertex_indices in enumerate(faces):
        if face_index < len(face_scores):
            score = face_scores[face_index]
            for vertex_index in vertex_indices:
                if vertex_index < num_vertices:
                    vertex_scores_sum[vertex_index] += score
                    vertex_face_counts[vertex_index] += 1
    return vertex_scores_sum / (vertex_face_counts + 1e-8)

# ==============================================================================
# 메인 실행부 (Unity Bridge)
# ==============================================================================
if __name__ == '__main__':
    # 1. Unity에서 보낸 인자 받기
    parser = argparse.ArgumentParser()
    parser.add_argument('--mesh', type=str, required=True, help='WSL path to .obj file')
    parser.add_argument('--tex', type=str, required=True, help='WSL path to texture file (not used directly in logic but passed for compatibility)')
    parser.add_argument('--out', type=str, required=True, help='WSL path to output directory')
    args = parser.parse_args()

    print(f"[Bridge] Start processing: {os.path.basename(args.mesh)}")

    # 2. Config 로드
    with open(CONFIG_PATH, 'r') as f:
        cfg = yaml.load(f, Loader=yaml.loader.SafeLoader)

    # 3. 데이터셋 경로 동적 수정 (핵심)
    # Unity가 보낸 파일이 있는 폴더를 데이터 루트로 설정해버립니다.
    mesh_dir = os.path.dirname(args.mesh)
    mesh_filename_no_ext = os.path.splitext(os.path.basename(args.mesh))[0]
    
    # MeshDataset이 해당 폴더에서 파일을 찾을 수 있게 Config 조작
    cfg['dataset']['data_root'] = mesh_dir 
    
    # 4. 모델 로드
    device = torch.device("cuda:0" if torch.cuda.is_available() else "cpu")
    
    if not os.path.exists(CKPT_PATH):
        print(f"[Error] Checkpoint not found: {CKPT_PATH}")
        sys.exit(1)

    model = MeshTextureNet(cfg=cfg)
    state_dict = torch.load(CKPT_PATH, map_location=device)
    new_state_dict = {}
    for k, v in state_dict.items():
        name = k[7:] if k.startswith('module.') else k 
        new_state_dict[name] = v
    model.load_state_dict(new_state_dict)
    model.to(device)
    model.eval()

    # 5. 데이터셋 생성 (단일 파일)
    # 리스트에 파일명 딱 하나만 넣어서 Dataset을 속입니다.
    try:
        predict_dataset = MeshDataset(cfg=cfg['dataset'], part='test', mesh_paths=[mesh_filename_no_ext])
        predict_loader = torch.utils.data.DataLoader(predict_dataset, batch_size=1, shuffle=False)
    except Exception as e:
        print(f"[Error] Failed to initialize Dataset: {e}")
        sys.exit(1)

    # 6. 추론 및 저장
    if not os.path.exists(args.out): os.makedirs(args.out)

    with torch.no_grad():
        for i, collated_dict in enumerate(predict_loader):
            mesh_name = collated_dict['mesh_name'][0]
            try:
                # 계산
                face_scores = get_saliency_for_single_mesh(model, device, collated_dict)
                
                # Face -> Vertex 변환
                faces_np = collated_dict['faces'].squeeze(0).cpu().numpy()
                if isinstance(collated_dict['verts'], torch.Tensor):
                    padded_len = collated_dict['verts'].shape[1] 
                else:
                    padded_len = collated_dict['verts'].shape[0]
                
                vertex_scores = convert_face_to_vertex_saliency(faces_np, face_scores, padded_len)
                
                # 원본 개수로 자르기 (Padding 제거)
                real_count = get_original_vertex_count(args.mesh)
                if real_count:
                    vertex_scores = vertex_scores[:real_count]
                
                # 정규화 (Min-Max)
                v_min, v_max = vertex_scores.min(), vertex_scores.max()
                if v_max - v_min > 1e-8:
                    vertex_scores = (vertex_scores - v_min) / (v_max - v_min)
                else:
                    vertex_scores = np.zeros_like(vertex_scores)
                
                # 결과 저장
                # 파일명 규칙: (Unity 코드와 맞춰야 함) 모델명_vertex_saliency.txt
                save_path = os.path.join(args.out, f"{mesh_name}_vertex_saliency.txt")
                np.savetxt(save_path, vertex_scores, fmt='%.6f')
                
                print(f"[Bridge] Saved: {save_path}")
                
            except Exception as e:
                print(f"[Error] Inference failed: {e}")
                sys.exit(1)

    print("[Bridge] Success")