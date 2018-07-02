using Celeste;
using Microsoft.Xna.Framework;
using Monocle;
using System;

namespace Celeste.Mod.Ghost.Net
{
    [Tracked(false)]
    public class GhostTouchSwitch : Entity
    {
        public static ParticleType P_Fire;

        public static ParticleType P_FireWhite;

        public int tIdx = 0; 

        public Switch Switch;

        private SoundSource touchSfx;

        private MTexture border = GFX.Game["objects/touchswitch/container"];

        private Sprite icon = new Sprite(GFX.Game, "objects/touchswitch/icon");

        private Color inactiveColor = Calc.HexToColor("5fcde4");

        private Color activeColor = Color.White;

        private Color finishColor = Calc.HexToColor("f141df");

        private float ease;

        private Wiggler wiggler;

        private Vector2 pulse = Vector2.One;

        private float timer;

        private BloomPoint bloom;

        private Level level => (Level)base.Scene;

        public GhostTouchSwitch(Vector2 position)
            : base(position)
        {
            base.Depth = 2000;
            base.Add(Switch = new Switch(false));
            base.Add(new PlayerCollider(OnPlayer, null, new Hitbox(30f, 30f, -15f, -15f)));
            base.Add(icon);
            base.Add(bloom = new BloomPoint(0f, 16f));
            bloom.Alpha = 0f;
            icon.Add("idle", "", 0f, default(int));
            icon.Add("spin", "", 0.1f, new Chooser<string>("spin", 1f), 0, 1, 2, 3, 4, 5);
            icon.Play("spin", false, false);
            icon.Color = inactiveColor;
            icon.CenterOrigin();
            base.Collider = new Hitbox(16f, 16f, -8f, -8f);
            base.Add(new TheoCrystalCollider(OnTheoCrystal, new Hitbox(20f, 20f, -10f, -10f)));
            base.Add(new SeekerCollider(OnSeeker, new Hitbox(24f, 24f, -12f, -12f)));
            Switch.OnActivate = delegate
            {
                wiggler.Start();
                for (int i = 0; i < 32; i++)
                {
                    float num = Calc.Random.NextFloat(6.28318548f);
                    //level.Particles.Emit(P_FireWhite, base.Position + Calc.AngleToVector(num, 6f), num);
                }
                icon.Rate = 4f;
            };
            Switch.OnFinish = delegate
            {
                ease = 0f;
            };
            Switch.OnStartFinished = delegate
            {
                icon.Rate = 0.1f;
                icon.Play("idle", false, false);
                icon.Color = finishColor;
                ease = 1f;
            };
            base.Add(wiggler = Wiggler.Create(0.5f, 4f, delegate (float v)
            {
                pulse = Vector2.One * (1f + v * 0.25f);
            }, false, false));
            base.Add(new VertexLight(Color.White, 0.8f, 16, 32));
            base.Add(touchSfx = new SoundSource());
        }

        public GhostTouchSwitch(EntityData data, Vector2 offset)
            : this(data.Position + offset)
        {
        }

        public void TurnOn()
        {
            if (!Switch.Activated)
            {
                touchSfx.Play("event:/game/general/touchswitch_any", null, 0f);
                if (Switch.Activate())
                {
                    SoundEmitter.Play("event:/game/general/touchswitch_last_oneshot");
                    base.Add(new SoundSource("event:/game/general/touchswitch_last_cutoff"));
                }
            }
        }

        private void OnPlayer(Player player)
        {
            TurnOn();
        }

        private void OnTheoCrystal(TheoCrystal theo)
        {
            TurnOn();
        }

        private void OnSeeker(Seeker seeker)
        {
            if (base.SceneAs<Level>().InsideCamera(base.Position, 10f))
            {
                TurnOn();
            }
        }

        public override void Update()
        {
            timer += Engine.DeltaTime * 8f;
            ease = Calc.Approach(ease, (Switch.Finished || Switch.Activated) ? 1f : 0f, Engine.DeltaTime * 2f);
            icon.Color = Color.Lerp(inactiveColor, Switch.Finished ? finishColor : activeColor, ease);
            Sprite sprite = icon;
            sprite.Color *= 0.5f + ((float)Math.Sin((double)timer) + 1f) / 2f * (1f - ease) * 0.5f + 0.5f * ease;
            bloom.Alpha = ease;
            if (Switch.Finished)
            {
                if (icon.Rate > 0.1f)
                {
                    icon.Rate -= 2f * Engine.DeltaTime;
                    if (icon.Rate <= 0.1f)
                    {
                        icon.Rate = 0.1f;
                        wiggler.Start();
                        icon.Play("idle", false, false);
                        level.Displacement.AddBurst(base.Position, 0.6f, 4f, 28f, 0.2f, null, null);
                    }
                }
                else if (base.Scene.OnInterval(0.03f))
                {
                    Vector2 position = base.Position + new Vector2(0f, 1f) + Calc.AngleToVector(Calc.Random.NextAngle(), 5f);
                    //level.ParticlesBG.Emit(P_Fire, position);
                }
            }
            base.Update();
        }

        public override void Render()
        {
            border.DrawCentered(base.Position + new Vector2(0f, -1f), Color.Black);
            border.DrawCentered(base.Position, icon.Color, pulse);
            base.Render();
        }
    }

}