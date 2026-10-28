using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 잔상형 모션 블러. Renderer2D의 Add Renderer Feature로 붙여서 쓴다.
///
/// 지난 화면을 텍스처에 기억해 두고, 매 프레임 "지금 화면"과 "지난 화면"을 섞어 출력한다.
/// 섞은 결과를 다시 기억해 두므로, 움직이는 것 뒤로 점점 옅어지는 꼬리가 생긴다.
/// 강도는 MotionAfterimage.Strength로 정하고, 0이면 아무것도 하지 않는다.
/// (2D 렌더러는 물체의 움직임 정보를 만들지 않아서 URP 기본 모션 블러가 동작하지 않는다)
/// </summary>
public class AfterimageRendererFeature : ScriptableRendererFeature
{
    [Tooltip("잔상을 섞는 셰이더. 비워 두면 ProjectZ/Afterimage를 찾아 쓴다.")]
    public Shader shader;

    Material material;
    AfterimagePass pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find("ProjectZ/Afterimage");
        if (shader != null) material = CoreUtils.CreateEngineMaterial(shader);

        pass = new AfterimagePass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 게임 화면에만 건다. (Scene 뷰나 미리보기 창과 기억해 둔 화면을 섞지 않게)
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        float persistence = MotionAfterimage.FramePersistence;
        if (material == null || persistence <= 0f)
        {
            // 꺼져 있는 동안의 화면은 기억하지 않으므로, 다시 켜질 때 새로 잡게 한다.
            pass.InvalidateHistory();
            return;
        }

        pass.Setup(material, persistence);
        pass.requiresIntermediateTexture = true;   // 화면을 읽어야 하므로 중간 텍스처가 필요하다.
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
    }

    class AfterimagePass : ScriptableRenderPass
    {
        static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        static readonly int HistoryId = Shader.PropertyToID("_AfterimageHistory");
        static readonly int AmountId = Shader.PropertyToID("_AfterimageAmount");

        readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        Material material;
        float persistence;
        RTHandle history;        // 지난 화면 (프레임이 바뀌어도 남아 있어야 해서 직접 들고 있는다)
        bool historyValid;

        class PassData
        {
            public Material material;
            public MaterialPropertyBlock properties;
            public TextureHandle source;
            public TextureHandle history;
            public float persistence;
        }

        public void Setup(Material material, float persistence)
        {
            this.material = material;
            this.persistence = persistence;
        }

        public void InvalidateHistory() => historyValid = false;

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            // 이미 화면에 바로 그려진 뒤라면 읽을 수 없으므로 건너뛴다.
            if (resources.isActiveTargetBackBuffer) return;

            TextureHandle source = resources.cameraColor;
            var resultDesc = renderGraph.GetTextureDesc(source);
            resultDesc.name = "_AfterimageResult";
            resultDesc.clearBuffer = false;
            TextureHandle result = renderGraph.CreateTexture(resultDesc);

            // 지난 화면을 담을 텍스처. 화면 크기가 바뀌면 새로 만들고 기억도 새로 잡는다.
            var historyDesc = cameraData.cameraTargetDescriptor;
            historyDesc.depthBufferBits = 0;
            historyDesc.depthStencilFormat = GraphicsFormat.None;
            historyDesc.msaaSamples = 1;
            if (RenderingUtils.ReAllocateHandleIfNeeded(ref history, historyDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_AfterimageHistory"))
            {
                historyValid = false;
            }
            TextureHandle historyHandle = renderGraph.ImportTexture(history);

            // 막 켜졌으면 지금 화면을 기억으로 삼는다. (오래된 화면이 비치지 않게)
            if (!historyValid)
            {
                renderGraph.AddBlitPass(source, historyHandle, Vector2.one, Vector2.zero, passName: "Afterimage Reset");
                historyValid = true;
            }

            // 지금 화면과 지난 화면을 섞는다.
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Afterimage Blend", out var data))
            {
                data.material = material;
                data.properties = properties;
                data.source = source;
                data.history = historyHandle;
                data.persistence = persistence;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(historyHandle, AccessFlags.Read);
                builder.SetRenderAttachment(result, 0, AccessFlags.Write);

                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                {
                    RTHandle sourceTexture = d.source;
                    RTHandle historyTexture = d.history;

                    d.properties.Clear();
                    d.properties.SetTexture(BlitTextureId, sourceTexture);
                    d.properties.SetTexture(HistoryId, historyTexture);
                    d.properties.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    d.properties.SetFloat(AmountId, d.persistence);

                    context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 0, MeshTopology.Triangles, 3, 1, d.properties);
                });
            }

            // 섞은 결과를 다음 프레임을 위해 기억해 두고, 화면 출력으로도 쓴다.
            renderGraph.AddBlitPass(result, historyHandle, Vector2.one, Vector2.zero, passName: "Afterimage Store");
            resources.cameraColor = result;
        }

        public void Dispose()
        {
            history?.Release();
            history = null;
        }
    }
}
