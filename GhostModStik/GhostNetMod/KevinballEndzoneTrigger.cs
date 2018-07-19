using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Ghost.Net
{
    [Tracked(false)]
    public class KevinballEndzoneTrigger : Trigger
    {
        public KevinballEndzoneTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
        }
    }

}