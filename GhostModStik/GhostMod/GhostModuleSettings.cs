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
    public class GhostModuleSettings : EverestModuleSettings {

        [SettingIgnore]
        public bool AlwaysShowSettings { get; set; } = false;

        public GhostModuleMode Mode { get; set; } = GhostModuleMode.On;

        [SettingInGame(false)]
        public string Name { get; set; } = "";

        [SettingIgnore] // Ignore on older builds of Everest which don't support custom entry creators.
        public string NameFilter { get; set; } = "";

        public bool ShowNames { get; set; } = true;

        public bool ShowDeaths { get; set; } = false;

        [SettingRange(0, 10)]
        public int InnerOpacity { get; set; } = 4;
        [YamlIgnore]
        [SettingIgnore]
        public float InnerOpacityFactor => InnerOpacity / 10f;

        [SettingRange(0, 10)]
        public int InnerHairOpacity { get; set; } = 4;
        [YamlIgnore]
        [SettingIgnore]
        public float InnerHairOpacityFactor => InnerHairOpacity / 10f;

        [SettingRange(0, 10)]
        public int OuterOpacity { get; set; } = 1;
        [YamlIgnore]
        [SettingIgnore]
        public float OuterOpacityFactor => OuterOpacity / 10f;

        [SettingRange(0, 10)]
        public int OuterHairOpacity { get; set; } = 1;
        [YamlIgnore]
        [SettingIgnore]
        public float OuterHairOpacityFactor => OuterHairOpacity / 10f;

        [SettingRange(0, 10)]
        public int InnerRadius { get; set; } = 4;
        [YamlIgnore]
        [SettingIgnore]
        public float InnerRadiusDist => InnerRadius * InnerRadius * 64f;

        [SettingRange(0, 10)]
        public int BorderSize { get; set; } = 4;
        [YamlIgnore]
        [SettingIgnore]
        public float BorderSizeDist => BorderSize * BorderSize * 64f;

        public void ShowNameFilterEntry(TextMenu menu, bool inGame) {
            // TODO: Create a slider to choose between all available names.
        }

    }
    public enum GhostModuleMode {
        Off = 0,
        Record = 1 << 0,
        Play = 1 << 1,
        On = Record | Play
    }
}
