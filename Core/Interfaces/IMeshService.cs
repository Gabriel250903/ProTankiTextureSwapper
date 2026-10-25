namespace TextureSwapper.Core.Interfaces
{
    public interface IMeshService
    {
        HashSet<string> ExtractTextureNames(byte[]? meshBytes);
        bool IsValidMesh(byte[]? meshBytes);
    }
}
