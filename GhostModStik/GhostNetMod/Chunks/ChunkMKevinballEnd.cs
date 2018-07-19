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
    /// Make an emote spawn above the player.
    /// </summary>
    public class ChunkMKevinballEnd : IChunk
    {

        public const string ChunkID = "nMkE";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint Winner; 

        public void Read(BinaryReader reader)
        {
            Winner = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Winner);
        }

        public object Clone()
            => new ChunkMKevinballEnd
            {
                Winner = Winner
            };

        public static implicit operator ChunkMKevinballEnd(GhostNetFrame frame)
            => frame.Get<ChunkMKevinballEnd>();

    }
}
