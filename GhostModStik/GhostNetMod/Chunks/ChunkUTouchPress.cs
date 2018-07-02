using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost.Net
{
    [Chunk(ChunkID)]
    /// <summary>
    /// Update chunk sent on (best case) each "collision" frame.
    /// A player always receives this with a HHead.PlayerID of the "colliding" player.
    /// </summary>
    public class ChunkUTouchPress : IChunk
    {

        public const string ChunkID = "nUtP";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint TIdx;

        public void Read(BinaryReader reader)
        {
            TIdx = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(TIdx);
        }

        public object Clone()
            => new ChunkUTouchPress
            {
                TIdx = TIdx
            };

        public static implicit operator ChunkUTouchPress(GhostNetFrame frame)
            => frame.Get<ChunkUTouchPress>();

    }
}
