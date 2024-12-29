namespace dynimp.Models;

public class ModuleImportDescriptor
{
    public uint OriginalFirstThunk { get; set; }
    public uint TimeDateStamp { get; set; }
    public uint ForwarderChain { get; set; }
    public uint Name { get; set; }
    public uint FirstThunk { get; set; }

    public bool Equals(ModuleImportDescriptor other)
    {
        return OriginalFirstThunk == other.OriginalFirstThunk &&
               TimeDateStamp == other.TimeDateStamp &&
               ForwarderChain == other.ForwarderChain &&
               Name == other.Name &&
               FirstThunk == other.FirstThunk;
    }
    
    public override bool Equals(object? obj)
    {
        return obj is ModuleImportDescriptor other && Equals(other);
    }
    
    public override int GetHashCode()
    {
        return HashCode.Combine(OriginalFirstThunk, TimeDateStamp, ForwarderChain, Name, FirstThunk);
    }
    
    public static bool operator ==(ModuleImportDescriptor left, ModuleImportDescriptor right)
    {
        return left.Equals(right);
    }
    
    public static bool operator !=(ModuleImportDescriptor left, ModuleImportDescriptor right)
    {
        return !left.Equals(right);
    }
    
    public override string ToString()
    {
        return $"OriginalFirstThunk: {OriginalFirstThunk}, TimeDateStamp: {TimeDateStamp}, ForwarderChain: {ForwarderChain}, Name: {Name}, FirstThunk: {FirstThunk}";
    }
    
    public static ModuleImportDescriptor FromBytes(byte[] bytes)
    {
        return new ModuleImportDescriptor
        {
            OriginalFirstThunk = BitConverter.ToUInt32(bytes, 0),
            TimeDateStamp = BitConverter.ToUInt32(bytes, 4),
            ForwarderChain = BitConverter.ToUInt32(bytes, 8),
            Name = BitConverter.ToUInt32(bytes, 12),
            FirstThunk = BitConverter.ToUInt32(bytes, 16)
        };
    }
    
    public byte[] ToBytes()
    {
        List<byte> bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(OriginalFirstThunk));
        bytes.AddRange(BitConverter.GetBytes(TimeDateStamp));
        bytes.AddRange(BitConverter.GetBytes(ForwarderChain));
        bytes.AddRange(BitConverter.GetBytes(Name));
        bytes.AddRange(BitConverter.GetBytes(FirstThunk));
        return bytes.ToArray();
    }
}