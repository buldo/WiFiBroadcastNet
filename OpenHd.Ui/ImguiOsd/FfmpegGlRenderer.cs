using Hexa.NET.OpenGL;
using SharpVideo.Decoding.Ffmpeg;

namespace OpenHd.Ui.ImguiOsd;

/// <summary>
/// OpenGL renderer for YUV420P video frames from FFmpeg using Hexa.NET.OpenGL
/// </summary>
internal sealed unsafe class FfmpegGlRenderer : IVideoRenderer, IDisposable
{
    private readonly ILogger<FfmpegGlRenderer> _logger;
    private readonly GL _gl;

    private uint _shaderProgram;
    private uint _vao;
    private uint _vbo;
    private uint _textureY;
    private uint _textureU;
    private uint _textureV;

    private int _videoWidth;
    private int _videoHeight;
    private bool _disposed;

    public FfmpegGlRenderer(GL gl, ILogger<FfmpegGlRenderer> logger)
    {
        _gl = gl;
        _logger = logger;

        InitializeGl();
    }

    private void InitializeGl()
    {
        _logger.LogInformation("Initializing OpenGL renderer for YUV video");

        _shaderProgram = CreateYuvShaderProgram();
        (_vao, _vbo) = CreateFullscreenQuad();

        _textureY = CreateTexture();
        _textureU = CreateTexture();
        _textureV = CreateTexture();

        _logger.LogInformation("OpenGL renderer initialized");
    }

    private uint CreateYuvShaderProgram()
    {
        const string vertexShaderSource = @"
#version 330 core
layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTexCoord;

out vec2 TexCoord;

void main()
{
    gl_Position = vec4(aPosition, 0.0, 1.0);
    TexCoord = aTexCoord;
}";

        const string fragmentShaderSource = @"
#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D texY;
uniform sampler2D texU;
uniform sampler2D texV;

void main()
{
    float y = texture(texY, TexCoord).r;
    float u = texture(texU, TexCoord).r - 0.5;
    float v = texture(texV, TexCoord).r - 0.5;
    
    // YUV to RGB conversion (BT.601)
    float r = y + 1.402 * v;
    float g = y - 0.344136 * u - 0.714136 * v;
    float b = y + 1.772 * u;
    
    FragColor = vec4(r, g, b, 1.0);
}";

        var vertexShader = _gl.CreateShader(GLShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexShaderSource);
        _gl.CompileShader(vertexShader);
        CheckShaderCompilation(vertexShader, "Vertex");

        var fragmentShader = _gl.CreateShader(GLShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentShaderSource);
        _gl.CompileShader(fragmentShader);
        CheckShaderCompilation(fragmentShader, "Fragment");

        var program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);
        CheckProgramLinking(program);

        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        _gl.UseProgram(program);
        _gl.Uniform1i(_gl.GetUniformLocation(program, "texY"), 0);
        _gl.Uniform1i(_gl.GetUniformLocation(program, "texU"), 1);
        _gl.Uniform1i(_gl.GetUniformLocation(program, "texV"), 2);

        return program;
    }

    private void CheckShaderCompilation(uint shader, string type)
    {
        int success;
        _gl.GetShaderiv(shader, GLShaderParameterName.CompileStatus, &success);
        if (success == 0)
        {
            var log = _gl.GetShaderInfoLog(shader);
            throw new Exception($"{type} shader compilation failed: {log}");
        }
    }

    private void CheckProgramLinking(uint program)
    {
        int success;
        _gl.GetProgramiv(program, GLProgramPropertyARB.LinkStatus, &success);
        if (success == 0)
        {
            var log = _gl.GetProgramInfoLog(program);
            throw new Exception($"Shader program linking failed: {log}");
        }
    }

    private (uint vao, uint vbo) CreateFullscreenQuad()
    {
        float[] vertices =
        [
            // Position    // TexCoord
            -1.0f,  1.0f,  0.0f, 0.0f,  // Top-left
            -1.0f, -1.0f,  0.0f, 1.0f,  // Bottom-left
             1.0f, -1.0f,  1.0f, 1.0f,  // Bottom-right
             1.0f,  1.0f,  1.0f, 0.0f   // Top-right
        ];

        var vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        var vbo = _gl.GenBuffer();
        _gl.BindBuffer(GLBufferTargetARB.ArrayBuffer, vbo);

        fixed (float* v = vertices)
        {
            _gl.BufferData(GLBufferTargetARB.ArrayBuffer, (nint)(vertices.Length * sizeof(float)),
                v, GLBufferUsageARB.StaticDraw);
        }

        _gl.VertexAttribPointer(0, 2, GLVertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);
        _gl.EnableVertexAttribArray(0);

        _gl.VertexAttribPointer(1, 2, GLVertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);

        return (vao, vbo);
    }

    private uint CreateTexture()
    {
        var texture = _gl.GenTexture();
        _gl.BindTexture(GLTextureTarget.Texture2D, texture);
        _gl.TexParameteri(GLTextureTarget.Texture2D, (GLTextureParameterName)0x2801, 0x2601); // GL_TEXTURE_MIN_FILTER, GL_LINEAR
        _gl.TexParameteri(GLTextureTarget.Texture2D, (GLTextureParameterName)0x2800, 0x2601); // GL_TEXTURE_MAG_FILTER, GL_LINEAR
        _gl.TexParameteri(GLTextureTarget.Texture2D, (GLTextureParameterName)0x2802, 0x812F); // GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE
        _gl.TexParameteri(GLTextureTarget.Texture2D, (GLTextureParameterName)0x2803, 0x812F); // GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE
        return texture;
    }

    public void UploadFrame(FfmpegDecodedFrame frame)
    {
        var avFrame = frame.Frame;

        if (_videoWidth != avFrame->width || _videoHeight != avFrame->height)
        {
            _videoWidth = avFrame->width;
            _videoHeight = avFrame->height;
            _logger.LogInformation("Video resolution: {Width}x{Height}", _videoWidth, _videoHeight);
        }

        _gl.ActiveTexture(GLTextureUnit.Texture0);
        _gl.BindTexture(GLTextureTarget.Texture2D, _textureY);
        _gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.R8,
            avFrame->width, avFrame->height, 0,
            GLPixelFormat.Red, GLPixelType.UnsignedByte, avFrame->data[0]);

        _gl.ActiveTexture(GLTextureUnit.Texture1);
        _gl.BindTexture(GLTextureTarget.Texture2D, _textureU);
        _gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.R8,
            avFrame->width / 2, avFrame->height / 2, 0,
            GLPixelFormat.Red, GLPixelType.UnsignedByte, avFrame->data[1]);

        _gl.ActiveTexture(GLTextureUnit.Texture2);
        _gl.BindTexture(GLTextureTarget.Texture2D, _textureV);
        _gl.TexImage2D(GLTextureTarget.Texture2D, 0, GLInternalFormat.R8,
            avFrame->width / 2, avFrame->height / 2, 0,
            GLPixelFormat.Red, GLPixelType.UnsignedByte, avFrame->data[2]);
    }

    public void Render()
    {
        _gl.ActiveTexture(GLTextureUnit.Texture0);
        _gl.BindTexture(GLTextureTarget.Texture2D, _textureY);
        _gl.ActiveTexture(GLTextureUnit.Texture1);
        _gl.BindTexture(GLTextureTarget.Texture2D, _textureU);
        _gl.ActiveTexture(GLTextureUnit.Texture2);
        _gl.BindTexture(GLTextureTarget.Texture2D, _textureV);

        _gl.UseProgram(_shaderProgram);

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(GLPrimitiveType.TriangleFan, 0, 4);
    }

    public bool HasFrame => _videoWidth > 0 && _videoHeight > 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _gl.DeleteTexture(_textureY);
            _gl.DeleteTexture(_textureU);
            _gl.DeleteTexture(_textureV);
            _gl.DeleteBuffer(_vbo);
            _gl.DeleteVertexArray(_vao);
            _gl.DeleteProgram(_shaderProgram);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error during OpenGL resource cleanup (context may be already destroyed)");
        }

        _disposed = true;
    }
}
