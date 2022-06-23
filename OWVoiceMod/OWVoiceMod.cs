﻿using OWML.ModHelper;
using OWML.Common;
using OWML.Utils;
using UnityEngine;
using System.IO;
using System.Linq;

namespace OWVoiceMod
{
    public class OWVoiceMod : ModBehaviour
    {
        private new static IModHelper ModHelper;
        private static GameObject player;
        private static AudioSource audioSource;
        private static NomaiTranslatorProp nomaiTranslatorProp;

        private static TextAsset xmlCharacterDialogueAsset;
        private static string characterName;
        private static string currentTextName;
        private static string currentAssetName;
        private static string oldTextName = null;

        private static bool splashSkip;
        private static bool conversations;
        private static bool hearthianRecordings;
        private static bool nomaiRecordings;
        private static bool paperNotes;
        private static bool nomaiScrolls;
        private static bool nomaiComputers;
        private static bool owlkWriting;
        private static float volume;
        private static bool loaded = false;

        private void Start()
        {
            ModHelper = base.ModHelper;

            if (splashSkip)
            {
                // Skip splash screen (from vesper's half life mod)
                // https://github.com/Vesper-Works/OuterWildsOnline/blob/master/OuterWildsOnline/ConnectionController.cs#L106-L119
                // Skip flash screen.
                var titleScreenAnimation = FindObjectOfType<TitleScreenAnimation>();
                titleScreenAnimation._fadeDuration = 0;
                titleScreenAnimation._gamepadSplash = false;
                titleScreenAnimation._introPan = false;
                titleScreenAnimation.Invoke("FadeInTitleLogo");

                // Skip menu fade.
                var titleAnimationController = FindObjectOfType<TitleAnimationController>();
                titleAnimationController._logoFadeDelay = 0.001f;
                titleAnimationController._logoFadeDuration = 0.001f;
                titleAnimationController._optionsFadeDelay = 0.001f;
                titleAnimationController._optionsFadeDuration = 0.001f;
                titleAnimationController._optionsFadeSpacing = 0.001f;
            }

            ModHelper.HarmonyHelper.AddPrefix<CharacterDialogueTree>("StartConversation", typeof(OWVoiceMod), nameof(OWVoiceMod.StartConversation));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("DisplayTextNode", typeof(OWVoiceMod), nameof(OWVoiceMod.DisplayTextNode));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("ClearNomaiText", typeof(OWVoiceMod), nameof(OWVoiceMod.ClearNomaiText));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("OnUnequipTool", typeof(OWVoiceMod), nameof(OWVoiceMod.OnUnequipTool));

            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        public override void Configure(IModConfig config)
        {
            splashSkip = config.GetSettingsValue<bool>("splashSkip");
            conversations = config.GetSettingsValue<bool>("conversations");
            hearthianRecordings = config.GetSettingsValue<bool>("hearthianRecordings");
            nomaiRecordings = config.GetSettingsValue<bool>("nomaiRecordings");
            paperNotes = config.GetSettingsValue<bool>("paperNotes");
            nomaiScrolls = config.GetSettingsValue<bool>("nomaiScrolls");
            nomaiComputers = config.GetSettingsValue<bool>("nomaiComputers");
            owlkWriting = config.GetSettingsValue<bool>("owlkWriting");
            volume = config.GetSettingsValue<float>("volume");
            if (loaded) audioSource.volume = volume;
        }

        private static void OnCompleteSceneLoad(OWScene orignalScene, OWScene loadScene)
        {
            if (loadScene is OWScene.Credits_Fast or OWScene.Credits_Final)
            {
                CreditsAsset creditsAsset = FindObjectOfType<Credits>()._creditsAsset;
                try { creditsAsset.xml = new TextAsset(File.ReadAllText(ModHelper.Manifest.ModFolderPath + "credits.bytes")); }
                catch { ModHelper.Console.WriteLine("Credits file not found!", MessageType.Error); }
                return;
            }
            else if (loadScene != OWScene.SolarSystem) return;

            player = GameObject.Find("Player_Body");
            audioSource = player.AddComponent<AudioSource>();
            audioSource.volume = volume;
            loaded = true;

            CharacterDialogueTree[] characterDialogueTree = Resources.FindObjectsOfTypeAll<CharacterDialogueTree>();
            foreach (CharacterDialogueTree childCharacterDialogueTree in characterDialogueTree)
            {
                childCharacterDialogueTree.OnAdvancePage += OnAdvancePage;
                childCharacterDialogueTree.OnEndConversation += OnEndConversation;
            }

            nomaiTranslatorProp = FindObjectOfType<NomaiTranslatorProp>();
        }

        private static void StartConversation(ref CharacterDialogueTree __instance)
        {
            xmlCharacterDialogueAsset = __instance._xmlCharacterDialogueAsset;
            characterName = __instance._characterName;
        }

        private static void OnAdvancePage(string nodeName, int pageNum)
        {
            if (!conversations && characterName != "NOTE" && characterName != "RECORDING") return;
            if (!hearthianRecordings && characterName == "RECORDING") return;
            if (!paperNotes && characterName == "NOTE") return;

            AudioUnload();

            string currentAssetName = xmlCharacterDialogueAsset.name + nodeName + pageNum.ToString();
            foreach (string assetPathS in Directory.EnumerateFiles(ModHelper.Manifest.ModFolderPath, "*.wav", SearchOption.AllDirectories))
            {
                string assetFileName = Path.GetFileNameWithoutExtension(assetPathS)
                    .Replace(" ", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .ToLower();
                if (assetFileName.Split('+').Any(x => x == currentAssetName.ToLower()))
                {
                    audioSource.clip = ModHelper.Assets.GetAudio(assetPathS.Substring(ModHelper.Manifest.ModFolderPath.Length));
                    if (volume > 0 && audioSource.clip != null) audioSource.Play();
                    break;
                }
            }
        }

        private static void OnEndConversation()
        {
            AudioUnload();
        }

        private static void DisplayTextNode()
        {
            NomaiText nomaiText = nomaiTranslatorProp._nomaiTextComponent;
            int currentTextID = nomaiTranslatorProp._currentTextID;
            if (nomaiText is NomaiComputer or NomaiVesselComputer)
            {
                if (!nomaiComputers) return;
                if (nomaiText.gameObject.TryGetComponent<NomaiWarpComputerLogger>(out _)) currentAssetName = "NomaiWarpComputer";
                else currentAssetName = nomaiText._nomaiTextAsset.name;
                currentTextName = currentAssetName + currentTextID.ToString();
            }
            else if (nomaiText is GhostWallText)
            {
                if (!owlkWriting) return;
                currentAssetName = "OwlkStatic";
                currentTextName = "OwlkStatic";
            }
            else
            {
                if (!nomaiScrolls && nomaiText is NomaiWallText) return;
                if (!nomaiRecordings && nomaiText is not NomaiWallText) return;
                currentAssetName = nomaiText._nomaiTextAsset.name;
                currentTextName = currentAssetName + currentTextID.ToString();
            }

            if (currentTextName != oldTextName)
            {
                AudioUnload();

                if (nomaiText.IsTranslated(currentTextID))
                {
                    foreach (string assetPathS in Directory.EnumerateFiles(ModHelper.Manifest.ModFolderPath, "*.wav", SearchOption.AllDirectories))
                    {
                        string assetFileName = Path.GetFileNameWithoutExtension(assetPathS)
                            .Replace(" ", "")
                            .Replace("(", "")
                            .Replace(")", "")
                            .ToLower();
                        if (assetFileName.Split('+').Any(x => x == currentTextName.ToLower()))
                        {
                            audioSource.clip = ModHelper.Assets.GetAudio(assetPathS.Substring(ModHelper.Manifest.ModFolderPath.Length));
                            if (volume > 0 && audioSource.clip != null) audioSource.Play();
                            break;
                        }
                    }

                    if (nomaiText is not GhostWallText) oldTextName = currentTextName;
                }
                else
                {
                    oldTextName = null;
                }
            }
        }

        private static void ClearNomaiText()
        {
            AudioUnload();
            oldTextName = null;
        }

        private static void OnUnequipTool()
        {
            AudioUnload();
            oldTextName = null;
        }

        private static void AudioUnload()
        {
            audioSource.Stop();
            if (audioSource.clip != null) audioSource.clip.UnloadAudioData();
            audioSource.clip = null;
        }
    }
}
