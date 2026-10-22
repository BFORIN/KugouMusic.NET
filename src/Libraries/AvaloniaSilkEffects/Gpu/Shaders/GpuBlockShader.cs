using System.Runtime.InteropServices;

namespace AvaloniaSilkEffects.Gpu.Shaders;

/// <summary>
/// A <see cref="GpuShader"/> for APIs with explicit uniform buffers: values are packed into each stage's std140
/// block (from SPIR-V reflection) when set, and textures are kept in the program's sampler order.
/// </summary>
internal abstract class GpuBlockShader : GpuShader
{
    private readonly Dictionary<string, Slot[]> _slots = [];
    private readonly Dictionary<string, int> _samplers = [];

    protected GpuBlockShader(GpuCompiledProgram program)
    {
        Program = program;
        Name = program.Name;
        VertexUniforms = new byte[program.Vertex.Uniforms?.Size ?? 0];
        FragmentUniforms = new byte[program.Fragment.Uniforms?.Size ?? 0];
        Add(program.Vertex.Uniforms, VertexUniforms);
        Add(program.Fragment.Uniforms, FragmentUniforms);
        for (var index = 0; index < program.Samplers.Count; index++) _samplers[program.Samplers[index]] = index;
        Textures = new GpuTexture?[program.Samplers.Count];
    }

    public GpuCompiledProgram Program { get; }
    public byte[] VertexUniforms { get; }
    public byte[] FragmentUniforms { get; }
    public GpuTexture?[] Textures { get; }
    /// <summary>Bumped on every uniform change, so backends can skip re-uploading unchanged blocks.</summary>
    public int Version { get; private set; }

    public override void SetFloats(string name, ReadOnlySpan<float> values, int components)
    {
        if (!TryFind(name, out var slots, out var element)) return;
        foreach (var slot in slots) Write(slot, element, values, components);
        Version++;
    }

    public override void SetInt(string name, int value)
    {
        if (!TryFind(name, out var slots, out var element)) return;
        ReadOnlySpan<float> values = [value];
        foreach (var slot in slots) Write(slot, element, values, 1);
        Version++;
    }

    public override void SetTexture(string name, GpuTexture? texture)
    {
        if (_samplers.TryGetValue(name, out var index)) Textures[index] = texture;
    }

    private bool TryFind(string name, out Slot[] slots, out int element)
    {
        element = 0;
        if (_slots.TryGetValue(name, out slots!)) return true;
        // "uColors[2]" addresses one element of an array uniform.
        var bracket = name.IndexOf('[');
        if (bracket > 0 && name[^1] == ']' && int.TryParse(name.AsSpan(bracket + 1, name.Length - bracket - 2), out element) &&
            _slots.TryGetValue(name[..bracket], out slots!))
            return true;
        slots = [];
        return false;
    }

    private static void Write(Slot slot, int element, ReadOnlySpan<float> values, int components)
    {
        var member = slot.Member;
        var rows = member.VectorSize;
        var columns = member.Columns;
        var perElement = rows * columns;
        var block = slot.Block.AsSpan();
        for (var source = 0; element < member.ArraySize && source + Math.Min(components, perElement) <= values.Length; element++)
        {
            var baseOffset = member.Offset + element * member.ArrayStride;
            for (var column = 0; column < columns; column++)
            {
                var offset = baseOffset + column * member.MatrixStride;
                for (var row = 0; row < rows; row++)
                {
                    var index = source + column * rows + row;
                    var value = index < values.Length ? values[index] : 0;
                    var target = block.Slice(offset + row * 4, 4);
                    if (member.Scalar == GpuUniformScalar.Int) MemoryMarshal.Write(target, (int)value);
                    else MemoryMarshal.Write(target, value);
                }
            }
            source += perElement;
            if (source >= values.Length) break;
        }
    }

    private void Add(GpuUniformBlock? block, byte[] storage)
    {
        if (block is null) return;
        foreach (var member in block.Members)
        {
            var slot = new Slot(member, storage);
            _slots[member.Name] = _slots.TryGetValue(member.Name, out var existing) ? [.. existing, slot] : [slot];
        }
    }

    private sealed record Slot(GpuUniformMember Member, byte[] Block);
}
