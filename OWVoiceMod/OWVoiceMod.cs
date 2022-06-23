using OWML.ModHelper;
using OWML.Common;
using OWML.Utils;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace OWVoiceMod
{
    public class OWVoiceMod : ModBehaviour
    {
        private new static IModHelper ModHelper;
        private static IDictionary<string, string> assetPaths = new Dictionary<string, string>();
        private static AudioSource audioSource;
        private static NomaiTranslatorProp nomaiTranslatorProp;

        private static TextAsset xmlCharacterDialogueAsset;
        private static string characterName;
        private static string currentTextName;
        private static string oldTextName;

        private static bool splashSkip;
        private static bool conversations;
        private static bool hearthianRecordings;
        private static bool nomaiRecordings;
        private static bool paperNotes;
        private static bool nomaiScrolls;
        private static bool nomaiComputers;
        private static bool owlkWriting;
        private static float volume;

        private void Start()
        {
            ModHelper = base.ModHelper;

            foreach (string assetPath in Directory.EnumerateFiles(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets"), "*.wav", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFiles(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets"), "*.mp3", SearchOption.AllDirectories)))
            {
                string assetFileName = Path.GetFileNameWithoutExtension(assetPath)
                    .Replace(" ", "")
                    .Replace("(", "")
                    .Replace(")", "")
                    .ToLower();
                foreach (string assetFileNamePart in assetFileName.Split('+'))
                {
                    assetPaths.Add(assetFileNamePart, assetPath);
                }
            }

            if (splashSkip)
            {
                // https://github.com/Vesper-Works/OuterWildsOnline/blob/master/OuterWildsOnline/ConnectionController.cs#L106-L119
                // Skip splash screen.
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
            if (audioSource != null) audioSource.volume = volume;
        }

        private static void OnCompleteSceneLoad(OWScene orignalScene, OWScene loadScene)
        {
            if (loadScene is OWScene.Credits_Fast or OWScene.Credits_Final)
            {
                CreditsAsset creditsAsset = FindObjectOfType<Credits>()._creditsAsset;
                try { creditsAsset.xml = new TextAsset(File.ReadAllText(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets", "credits.bytes"))); }
                catch { ModHelper.Console.WriteLine("Credits file not found!", MessageType.Error); }
            }
            else if (loadScene is OWScene.SolarSystem or OWScene.EyeOfTheUniverse)
            {
                // gives time for Start to run
                ModHelper.Events.Unity.FireOnNextUpdate(() =>
                {
                    audioSource = Locator.GetPlayerBody().gameObject.AddComponent<AudioSource>();
                    audioSource.volume = volume;
                    audioSource.outputAudioMixerGroup = Locator.GetAudioMixer().GetAudioMixerGroup(OWAudioMixer.TrackName.Environment);

                    CharacterDialogueTree[] characterDialogueTrees = Resources.FindObjectsOfTypeAll<CharacterDialogueTree>();
                    foreach (CharacterDialogueTree characterDialogueTree in characterDialogueTrees)
                    {
                        characterDialogueTree.OnAdvancePage += OnAdvancePage;
                        characterDialogueTree.OnEndConversation += OnEndConversation;
                    }

                    nomaiTranslatorProp = FindObjectOfType<NomaiTranslatorProp>();
                });
            }
        }

        private static void StartConversation(CharacterDialogueTree __instance)
        {
            xmlCharacterDialogueAsset = __instance._xmlCharacterDialogueAsset;
            characterName = __instance._characterName;
        }

        private static void OnAdvancePage(string nodeName, int pageNum)
        {
            if (!conversations && characterName != "NOTE" && characterName != "RECORDING") return;
            if (!hearthianRecordings && characterName == "RECORDING") return;
            if (!paperNotes && characterName == "NOTE") return;

            UnloadAudio();

            string currentAssetName = xmlCharacterDialogueAsset.name + nodeName + pageNum.ToString();
            LoadAudio(currentAssetName);
        }

        private static void OnEndConversation()
        {
            UnloadAudio();
        }

        private static void DisplayTextNode()
        {
            NomaiText nomaiText = nomaiTranslatorProp._nomaiTextComponent;
            int currentTextID = nomaiTranslatorProp._currentTextID;
            string currentAssetName;
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
                UnloadAudio();

                if (nomaiText.IsTranslated(currentTextID))
                {
                    LoadAudio(currentTextName);

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
            UnloadAudio();
            oldTextName = null;
        }

        private static void OnUnequipTool()
        {
            UnloadAudio();
            oldTextName = null;
        }

        private static void LoadAudio(string assetName)
        {
            ModHelper.Console.WriteLine($"Attempting to find audio for {assetName}...");
            assetName = assetName.ToLower();
            if (assetPaths.ContainsKey(assetName))
            {
                ModHelper.Console.WriteLine($"Found audio for {assetName}!", MessageType.Success);
                audioSource.clip = ModHelper.Assets.GetAudio(assetPaths[assetName].Substring(ModHelper.Manifest.ModFolderPath.Length));
                if (volume > 0 && audioSource.clip != null) audioSource.Play();
            } else
            {
                ModHelper.Console.WriteLine($"Couldn't find audio for {assetName}!", MessageType.Error);
            }
        }

        private static void UnloadAudio()
        {
            audioSource.Stop();
            if (audioSource.clip != null) Destroy(audioSource.clip);
            audioSource.clip = null;
        }
    }
}
