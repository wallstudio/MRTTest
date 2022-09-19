using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Collections.LowLevel.Unsafe;

[CreateAssetMenu(menuName = "MyRenderer")]
public class MyRenderPipelineAsset : RenderPipelineAsset
{
    [SerializeField] public Shader BlitCopy;
    protected override RenderPipeline CreatePipeline() => new MyRender(this);
}

public class MyRender : RenderPipeline
{
    readonly ShaderTagId m_ShaderTag = new ShaderTagId("SRPDefaultUnlit");
    readonly int _Depth = Shader.PropertyToID(nameof(_Depth));
    readonly int _Color0 = Shader.PropertyToID(nameof(_Color0)), _Color1 = Shader.PropertyToID(nameof(_Color1)), _Color2 = Shader.PropertyToID(nameof(_Color2));
    readonly ProfilingSampler m_DrawSampler = new ProfilingSampler($"{nameof(MyRender)}_Draw");
    readonly ProfilingSampler m_FinalBlitSampler = new ProfilingSampler($"{nameof(MyRender)}_FinalBlit");
    readonly Material m_Material;
    readonly Mesh m_Mesh = new Mesh()
    {
        vertices = new [] { new Vector3(-1, -1, 0), new Vector3(+1, -1, 0), new Vector3(-1, +1, 0), new Vector3(+1, +1, 0), },
        uv = new [] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1), },
        triangles = new [] { 0, 2, 1, 3, 1, 2 },
        hideFlags = HideFlags.HideAndDontSave,
    };

    public MyRender(MyRenderPipelineAsset asset) => m_Material = CoreUtils.CreateEngineMaterial(asset.BlitCopy);

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        foreach (var camera in cameras)
        {
            Render(context, camera);
        }
        context.Submit();
    }

    void Render(ScriptableRenderContext context, Camera camera)
    {
        camera.TryGetCullingParameters(false, out var cullingParameters);
        var cullResults = context.Cull(ref cullingParameters);
        var drawSettings = new DrawingSettings(m_ShaderTag, new SortingSettings(camera));
        var filterSettings = new FilteringSettings(RenderQueueRange.all);
        context.SetupCameraProperties(camera);

        // Alloc
        using (var cmd = new CommandsScope(context))
        {
            cmd.Cmd.GetTemporaryRT(_Depth, camera.pixelWidth, camera.pixelHeight, 24, FilterMode.Bilinear, GraphicsFormat.None);
            cmd.Cmd.GetTemporaryRT(_Color0, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
            cmd.Cmd.GetTemporaryRT(_Color1, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
            cmd.Cmd.GetTemporaryRT(_Color2, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm);
        }

        // Draw
        using var attachments = (stackalloc[]
        {
            new AttachmentDescriptor(GraphicsFormat.D24_UNorm)
            {
                loadStoreTarget = _Depth,
                loadAction = RenderBufferLoadAction.Clear, storeAction = RenderBufferStoreAction.Store, 
                clearColor = Color.clear, clearDepth = 1, clearStencil = 0,
            },
            new AttachmentDescriptor(GraphicsFormat.R8G8B8A8_UNorm)
            {
                loadStoreTarget = _Color0,
                loadAction = RenderBufferLoadAction.Clear, storeAction = RenderBufferStoreAction.Store,
                clearColor = Color.yellow, clearDepth = 1, clearStencil = 0,
            },
            new AttachmentDescriptor(GraphicsFormat.R8G8B8A8_UNorm)
            {
                loadStoreTarget = _Color1,
                loadAction = RenderBufferLoadAction.Clear, storeAction = RenderBufferStoreAction.Store,
                clearColor = Color.cyan, clearDepth = 1, clearStencil = 0,
            },
            new AttachmentDescriptor(GraphicsFormat.R8G8B8A8_UNorm)
            {
                loadStoreTarget = _Color2,
                loadAction = RenderBufferLoadAction.Clear, storeAction = RenderBufferStoreAction.Store,
                clearColor = Color.magenta, clearDepth = 1, clearStencil = 0,
            },
        }).ToNativeArray(Allocator.Temp);
        using (context.BeginScopedRenderPass(camera.pixelWidth, camera.pixelHeight, 1, attachments, 0))
        {
            using var colors = (stackalloc[] { 1, 2, 3, }).ToNativeArray(Allocator.Temp);
            using (context.BeginScopedSubPass(colors))
            {
                using (var cmd = new CommandsScope(context)) { m_DrawSampler.Begin(cmd.Cmd); }
                
                context.DrawRenderers(cullResults, ref drawSettings, ref filterSettings);
                
                using (var cmd = new CommandsScope(context)) { m_DrawSampler.End(cmd.Cmd); }
            }
        }

        // FinalBlit
        using (var cmd = new CommandsScope(context))
        using (new ProfilingScope(cmd.Cmd, m_FinalBlitSampler))
        {
            if(camera.cameraType is CameraType.Game)
            {
                cmd.Cmd.SetRenderTarget(RenderTargetHandle.CameraTarget.Identifier());
                cmd.Cmd.SetGlobalTexture("_MainTex", _Color0);
                cmd.Cmd.DrawMesh(m_Mesh, Matrix4x4.TRS(new Vector3(-0.5f, +0.5f, 0), Quaternion.identity, Vector3.one * 0.5f), m_Material);
                cmd.Cmd.SetGlobalTexture("_MainTex", _Color1);
                cmd.Cmd.DrawMesh(m_Mesh, Matrix4x4.TRS(new Vector3(+0.5f, +0.5f, 0), Quaternion.identity, Vector3.one * 0.5f), m_Material);
                cmd.Cmd.SetGlobalTexture("_MainTex", _Color2);
                cmd.Cmd.DrawMesh(m_Mesh, Matrix4x4.TRS(new Vector3(-0.5f, -0.5f, 0), Quaternion.identity, Vector3.one * 0.5f), m_Material);
                cmd.Cmd.SetGlobalTexture("_MainTex", _Depth);
                cmd.Cmd.DrawMesh(m_Mesh, Matrix4x4.TRS(new Vector3(+0.5f, -0.5f, 0), Quaternion.identity, Vector3.one * 0.5f), m_Material);
            }
            else
            {
                cmd.Cmd.Blit(_Color0, RenderTargetHandle.CameraTarget.Identifier());
            }
        }

        // Free
        using (var cmd = new CommandsScope(context))
        {
            cmd.Cmd.ReleaseTemporaryRT(_Depth);
            cmd.Cmd.ReleaseTemporaryRT(_Color0);
            cmd.Cmd.ReleaseTemporaryRT(_Color1);
            cmd.Cmd.ReleaseTemporaryRT(_Color2);
        }
    }
}


struct CommandsScope : IDisposable
{
    public ScriptableRenderContext Context { get; }
    public CommandBuffer Cmd { get; }
    
    public CommandsScope(ScriptableRenderContext context) => (Context, Cmd) = (context, CommandBufferPool.Get());
    
    public void Dispose()
    {
        Context.ExecuteCommandBuffer(Cmd);
        Cmd.Clear();
        CommandBufferPool.Release(Cmd);
    }
}


static class Utility
{
    public unsafe static NativeArray<T> ToNativeArray<T>(this Span<T> span, Allocator allocator) where T : unmanaged
    {
        var nativeArray = new NativeArray<T>(span.Length, allocator, NativeArrayOptions.UninitializedMemory);
        var pNativeArray = NativeArrayUnsafeUtility.GetUnsafePtr(nativeArray);
        fixed (T* pSource = &span.GetPinnableReference())
            Buffer.MemoryCopy(pSource, pNativeArray, nativeArray.Length * sizeof(T), span.Length * sizeof(T));
        return nativeArray;
    }
}
