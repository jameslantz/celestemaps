﻿using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost {
    // We need this to work across scenes.
    public class GhostInputReplayer : GameComponent {

        public GhostData Data;
        public int FrameIndex = 0;
        public GhostFrame Frame => Data == null ? default(GhostFrame) : Data[FrameIndex];
        public GhostFrame PrevFrame => Data == null ? default(GhostFrame) : Data[FrameIndex - 1];

        public GhostInputReplayer(Game game, GhostData data)
            : base(game) {
            Data = data;

            On.Celeste.Input.Initialize += HookInput;
            HookInput(null);
        }

        public void HookInput(On.Celeste.Input.orig_Initialize orig) {
            orig?.Invoke();

            Input.MoveX.Nodes.Add(new GhostInputNodes.MoveX(this));
            Input.MoveY.Nodes.Add(new GhostInputNodes.MoveY(this));

            Input.Aim.Nodes.Add(new GhostInputNodes.Aim(this));
            Input.MountainAim.Nodes.Add(new GhostInputNodes.MountainAim(this));

            Input.ESC.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.ESC));
            Input.Pause.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.Pause));
            Input.MenuLeft.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.MenuLeft));
            Input.MenuRight.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.MenuRight));
            Input.MenuUp.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.MenuUp));
            Input.MenuDown.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.MenuDown));
            Input.MenuConfirm.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.MenuConfirm));
            Input.MenuCancel.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.MenuCancel));
            Input.MenuJournal.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.MenuJournal));
            Input.QuickRestart.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.QuickRestart));
            Input.Jump.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.Jump));
            Input.Dash.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.Dash));
            Input.Grab.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.Grab));
            Input.Talk.Nodes.Add(new GhostInputNodes.Button(this, GhostChunkInput.ButtonMask.Talk));

            Logger.Log("ghost", "GhostReplayer hooked input.");
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            do {
                FrameIndex++;
            } while (
                (!Frame.Input.IsValid && FrameIndex < Data.Frames.Count) // Skip any frames not containing the input chunk.
            );

            if (Data == null || FrameIndex >= Data.Frames.Count)
                Remove();
        }

        public void Remove() {
            On.Celeste.Input.Initialize -= HookInput;
            Input.Initialize();
            Logger.Log("ghost", "GhostReplayer returned input.");
            Game.Components.Remove(this);
        }

    }
}
