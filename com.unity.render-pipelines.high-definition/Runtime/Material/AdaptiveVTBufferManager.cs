using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.HighDefinition
{
    class AdaptiveVTBufferManager
    {
        public static TextureHandle CreateAdaptiveFeedbackBuffer(RenderGraph renderGraph)
        {
#if UNITY_2020_2_OR_NEWER
            FastMemoryDesc colorFastMemDesc;
            colorFastMemDesc.inFastMemory = true;
            colorFastMemDesc.residencyFraction = 1.0f;
            colorFastMemDesc.flags = FastMemoryFlags.SpillTop;
#endif

            return renderGraph.CreateTexture(
                new TextureDesc(Vector2.one, true, true)
                {
                    colorFormat = GetFeedbackBufferFormat(),
                    enableRandomWrite = true,
                    bindTextureMS = false,
                    msaaSamples = MSAASamples.None,
                    clearBuffer = true,
                    clearColor = Color.white,
                    name = "AdaptiveVTFeedback",
                    fallBackToBlackTexture = true
#if UNITY_2020_2_OR_NEWER
                    ,
                    fastMemoryDesc = colorFastMemDesc
#endif
                }); ;;
        }

        public static GraphicsFormat GetFeedbackBufferFormat()
        {
            return GraphicsFormat.R8G8B8A8_UNorm;
        }

        const int kResolveScaleFactor = 16;

        Vector2 m_ResolverScale = new Vector2(1.0f / (float)kResolveScaleFactor, 1.0f / (float)kResolveScaleFactor);
        RTHandle m_LowresResolver;
        ComputeShader m_DownSampleCS = null;
        int m_DownsampleKernel;

        public AdaptiveVTBufferManager()
        {

            // This texture needs to be persistent because we do async gpu readback on it.
            m_LowresResolver = RTHandles.Alloc(m_ResolverScale, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, enableRandomWrite: true, autoGenerateMips: false, name: "AdaptiveVTFeedback lowres");
        }

        public void Cleanup()
        {        
            RTHandles.Release(m_LowresResolver);
            m_LowresResolver = null;
        }

        class ResolveAdaptiveVTData
        {
            public int width, height;
            public int lowresWidth, lowresHeight;
            public ComputeShader downsampleCS;
            public int downsampleKernel;

            public TextureHandle input;
            public TextureHandle lowres;
        }

        public void Resolve(RenderGraph renderGraph, HDCamera hdCamera, TextureHandle input)
        {
            if (hdCamera.frameSettings.IsEnabled(FrameSettingsField.VirtualTexturing))
            {
                if (m_DownSampleCS == null)
                {
                    m_DownSampleCS = HDRenderPipeline.currentAsset.renderPipelineResources.shaders.VTFeedbackDownsample;
                    m_DownsampleKernel = m_DownSampleCS.FindKernel("KMain");
                }

                using (var builder = renderGraph.AddRenderPass<ResolveAdaptiveVTData>("Resolve AdaptiveVT", out var passData, ProfilingSampler.Get(HDProfileId.VTFeedbackDownsample)))
                {
                    // The output is never read outside the pass but is still useful for the VT system so we can't cull this pass.
                    builder.AllowPassCulling(false);

                    bool msaa = hdCamera.msaaEnabled;
                    passData.width = hdCamera.actualWidth;
                    passData.height = hdCamera.actualHeight;
                    passData.lowresWidth = passData.width;
                    passData.lowresHeight = passData.height;
                    GetResolveDimensions(ref passData.lowresWidth, ref passData.lowresHeight);
                    passData.downsampleCS = m_DownSampleCS;
                    passData.downsampleKernel = m_DownsampleKernel;

                    passData.input = builder.ReadTexture(input);
                    passData.lowres = builder.WriteTexture(renderGraph.ImportTexture(m_LowresResolver));

                    builder.SetRenderFunc(
                        (ResolveAdaptiveVTData data, RenderGraphContext ctx) =>
                        {
                            RTHandle lowresBuffer = data.lowres;
                            RTHandle buffer = data.input;

                            Debug.Assert(data.lowresWidth <= lowresBuffer.referenceSize.x && data.lowresHeight <= lowresBuffer.referenceSize.y);

                            int inputID = (buffer.isMSAAEnabled) ? HDShaderIDs._InputTextureMSAA : HDShaderIDs._InputTexture;

                            ctx.cmd.SetComputeTextureParam(data.downsampleCS, data.downsampleKernel, inputID, buffer);
                            ctx.cmd.SetComputeTextureParam(data.downsampleCS, data.downsampleKernel, HDShaderIDs._OutputTexture, lowresBuffer);
                            var resolveCounter = 0;
                            var startOffsetX = (resolveCounter % kResolveScaleFactor);
                            var startOffsetY = (resolveCounter / kResolveScaleFactor) % kResolveScaleFactor;
                            ctx.cmd.SetComputeVectorParam(data.downsampleCS, HDShaderIDs._Params, new Vector4(kResolveScaleFactor, startOffsetX, startOffsetY, /*unused*/ -1));
                            ctx.cmd.SetComputeVectorParam(data.downsampleCS, HDShaderIDs._Params1, new Vector4(data.width, data.height, data.lowresWidth, data.lowresHeight));
                            var TGSize = 8; //Match shader
                            ctx.cmd.DispatchCompute(data.downsampleCS, data.downsampleKernel, ((int)data.lowresWidth + (TGSize - 1)) / TGSize, ((int)data.lowresHeight + (TGSize - 1)) / TGSize, 1);

                            ctx.cmd.ProcessAdaptiveVTFeedback(lowresBuffer, 0, data.lowresWidth, 0, data.lowresHeight, 0, 0);

                        });
                }
            }
        }

        void GetResolveDimensions(ref int w, ref int h)
        {
            w = Mathf.Max(Mathf.RoundToInt(m_ResolverScale.x * w), 1);
            h = Mathf.Max(Mathf.RoundToInt(m_ResolverScale.y * h), 1);
        }

    }
}
