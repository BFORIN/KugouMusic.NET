using Avalonia;
using AvaloniaSilkEffects.Gpu;

namespace AvaloniaSilkEffects.Backgrounds;

public sealed class LatentBackgroundScene : EffectScene
{
    private const string Vertex = """
        #version 330 core
        out vec2 v_objectUV;
        out vec2 vUv;
        uniform vec2 u_resolution;
        void main() {
            vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            vUv = p;
            v_objectUV = (p-.5)*u_resolution/min(u_resolution.x,u_resolution.y);
            gl_Position = vec4(p*2.-1.,0.,1.);
        }
        """;
    private const string Composite = """
        #version 330 core
        in vec2 vUv;
        uniform sampler2D uMesh;
        uniform sampler2D uDither;
        uniform vec3 uBackground;
        uniform float uPower, uBass, uMid;
        out vec4 fragColor;
        float soft(float b,float s) {
            float d = b <= .25 ? ((16.*b-12.)*b+4.)*b : sqrt(b);
            return s <= .5 ? b-(1.-2.*s)*b*(1.-b) : b+(2.*s-1.)*(d-b);
        }
        void main() {
            vec3 b = texture(uMesh,(vUv-.5)/(1.025+uPower*.018)+.5).rgb;
            float l = dot(b,vec3(.213,.715,.072));
            b = clamp(mix(vec3(l),b,1.04+uMid*.34)*( .94+uPower*.16),0.,1.);
            vec3 s = texture(uDither,(vUv-.5)/(1.015+uBass*.025)+.5).rgb;
            vec3 blended = vec3(soft(b.r,s.r),soft(b.g,s.g),soft(b.b,s.b));
            b = mix(b,blended,min(1.,.55+uBass*.25));
            fragColor = vec4(mix(b,uBackground,.35),1.);
        }
        """;

    public LatentAudio Audio { get; set; } = new();
    public LatentPalette Palette { get; set; } = LatentPalette.Midnight;
    private GpuDevice _gpu = null!;
    private GpuShader? _mesh, _dither, _composite;
    private GpuRenderTarget? _meshBuffer, _ditherBuffer;
    private double _scaling = 1;
    private LatentModulation _motion = new();
    private LatentPalette? _lastPalette;
    private EffectColor[] _colors = [];
    private static readonly string[] ColorUniforms =
        ["u_colors[0]","u_colors[1]","u_colors[2]","u_colors[3]","u_colors[4]","u_colors[5]"];

    public override void Initialize(EffectDevice device)
    {
        base.Initialize(device);
        _gpu = device.Gpu;
        // Previous handles can belong to a lost context; never delete them here.
        _mesh = _dither = _composite = null;
        _meshBuffer = _ditherBuffer = null;
        try
        {
            _mesh = _gpu.CreateShader(Vertex,LatentShaders.Mesh,"Folia latent mesh");
            _dither = _gpu.CreateShader(Vertex,LatentShaders.Dither,"Folia latent dither");
            _composite = _gpu.CreateShader(Vertex,Composite,"Folia latent soft-light");
            _meshBuffer = _gpu.CreateRenderTarget(depthStencil: false);
            _ditherBuffer = _gpu.CreateRenderTarget(depthStencil: false);
        }
        catch { DisposeGpuResources(); throw; }
    }

    public override void Resize(PixelSize size, double scaling) => _scaling = scaling;
    public override void Update(in EffectFrame frame) => _motion.Step(Audio,frame.Delta.TotalSeconds);

    public override void Render(EffectRenderContext context)
    {
        if (_mesh == null || _dither == null || _composite == null) return;
        var palette = Palette;
        if (!ReferenceEquals(_lastPalette,palette))
        {
            _lastPalette = palette;
            _colors = palette.MeshColors();
        }
        var size = context.PixelSize;
        var factor = Math.Min(1,Math.Sqrt(1280d*720/(size.Width*(double)size.Height)));
        var w = Math.Max(1,(int)Math.Round(size.Width*factor));
        var h = Math.Max(1,(int)Math.Round(size.Height*factor));
        var gpu = _gpu;
        var target = gpu.Target!;
        var viewport = gpu.Viewport;
        gpu.SetBlend(GpuBlend.None);
        try
        {
            _meshBuffer!.EnsureSize(w,h);
            _ditherBuffer!.EnsureSize(w,h);
            DrawPass(_mesh,_meshBuffer,w,h,(float)_motion.MeshTime);
            _mesh.Set("u_colorsCount",6);
            for (var i=0;i<6;i++) _mesh.Set(ColorUniforms[i],_colors[i]);
            _mesh.Set("u_distortion",.8f+_motion.Power*.62f);
            _mesh.Set("u_swirl",.1f+_motion.Mid*.38f);
            _mesh.Set("u_grainMixer",0f);
            _mesh.Set("u_grainOverlay",.01f);
            gpu.DrawFullscreen(_mesh);

            DrawPass(_dither,_ditherBuffer,w,h,(float)_motion.DitherTime);
            _dither.Set("u_pixelRatio",(float)(_scaling*w/size.Width));
            _dither.Set("u_originX",.5f); _dither.Set("u_originY",.5f);
            _dither.Set("u_scale",1f); _dither.Set("u_fit",0f);
            _dither.Set("u_pxSize",Math.Max(.5f,2.5f-_motion.Bass*2.5f*.34f));
            _dither.Set("u_shape",2f); _dither.Set("u_type",3f);
            _dither.Set("u_colorBack",palette.DitheringBackground);
            _dither.Set("u_colorFront",_colors[0]);
            gpu.DrawFullscreen(_dither);

            gpu.SetTarget(target);
            gpu.SetViewport(new GpuRect(0,0,size.Width,size.Height));
            _composite.SetTexture("uMesh",_meshBuffer.Texture);
            _composite.SetTexture("uDither",_ditherBuffer.Texture);
            _composite.Set("uBackground",palette.Background.R,palette.Background.G,palette.Background.B);
            _composite.Set("uPower",_motion.Power); _composite.Set("uBass",_motion.Bass); _composite.Set("uMid",_motion.Mid);
            gpu.DrawFullscreen(_composite);
            context.Primitives.RecordExternalDrawCalls(3);
        }
        finally
        {
            gpu.SetTarget(target);
            gpu.SetViewport(viewport);
        }
    }

    private void DrawPass(GpuShader program, GpuRenderTarget buffer,int w,int h,float time)
    {
        _gpu.SetTarget(buffer);
        _gpu.SetViewport(new GpuRect(0,0,w,h));
        program.Set("u_resolution",(float)w,(float)h);
        program.Set("u_time",time);
    }

    public override void DisposeGpuResources()
    {
        _mesh?.Dispose(); _dither?.Dispose(); _composite?.Dispose();
        _meshBuffer?.Dispose(); _ditherBuffer?.Dispose();
        _mesh = _dither = _composite = null;
        _meshBuffer = _ditherBuffer = null;
    }
}
