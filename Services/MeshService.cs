using TextureSwapper.Core.Interfaces;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class MeshService : IMeshService
    {
        public HashSet<string> ExtractTextureNames(byte[]? meshBytes)
        {
            return Mesh3dsHelper.ExtractTextureNames(meshBytes);
        }

        public bool IsValidMesh(byte[]? meshBytes)
        {
            return meshBytes != null && meshBytes.Length >= 6;
        }
    }
}
