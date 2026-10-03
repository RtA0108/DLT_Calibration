using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NormalsTextureFeature : ScriptableRendererFeature
{
    class NormalsPass : ScriptableRenderPass
    {
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // 이 패스는 실제로 아무것도 하지 않지만, _CameraNormalsTexture 생성을 트리거함
        }
    }

    NormalsPass normalsPass;

    public override void Create()
    {
        normalsPass = new NormalsPass
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(normalsPass);
    }
}
