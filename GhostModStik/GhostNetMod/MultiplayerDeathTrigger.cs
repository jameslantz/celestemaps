using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.Ghost.Net
{
    [Tracked(false)]
    public class MultiplayerDeathTrigger : Trigger
    {
        public MultiplayerDeathTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
        }
    }

}