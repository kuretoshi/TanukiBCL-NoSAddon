using System.IO;
using Nebula.Utilities;
using UnityEngine;

namespace TanukiBCL.Roles;

// A dedicated world prop; never borrow the placement console's sprite (which
// may be a ladder, cable or another small part of a vanilla task).
public static class VoiceAuthenticationTerminalVisual
{
    private static Sprite? sprite;
    public static Sprite Sprite
    {
        get
        {
            if (sprite != null) return sprite;
            using var stream = Virial.NebulaAPI.GetAddon("tanuki.bcl.roles")?.GetResource("VoiceAuthenticationTerminal.png")?.AsStream()
                ?? throw new FileNotFoundException("Missing voice authentication terminal artwork");
            using var data = new MemoryStream();
            stream.CopyTo(data);
            var texture = new Texture2D(2,2,TextureFormat.RGBA32,false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            GraphicsHelper.LoadImage(texture,data.ToArray(),false);
            sprite = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),texture.width);
            sprite.name = "TanukiVoiceAuthenticationTerminal";
            return sprite;
        }
    }
}
