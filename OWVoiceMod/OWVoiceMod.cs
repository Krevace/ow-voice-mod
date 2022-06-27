using NAudio.Wave;
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
        private static readonly Dictionary<string, string> assetPaths = new();
        private static string assetsFolder;
        private static string creditsAssetPath;
        private static AudioSource audioSource;
        private static NomaiTranslatorProp nomaiTranslatorProp;

        private static TextAsset xmlCharacterDialogueAsset;
        private static string characterName;
        private static string currentTextName;
        private static string oldTextName;
        private static int randomDialogueNum = -1;

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

            assetsFolder = Path.Combine(ModHelper.Manifest.ModFolderPath, "assets");
            RegisterAssets(assetsFolder);

            if (splashSkip)
            {
                // Copied from https://github.com/Vesper-Works/OuterWildsOnline/blob/master/OuterWildsOnline/ConnectionController.cs#L106-L119
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

            ModHelper.HarmonyHelper.AddPrefix<DialogueText>(nameof(DialogueText.GetDisplayStringList), typeof(OWVoiceMod), nameof(GetDisplayStringList));
            ModHelper.HarmonyHelper.AddPrefix<CharacterDialogueTree>(nameof(CharacterDialogueTree.StartConversation), typeof(OWVoiceMod), nameof(StartConversation));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.DisplayTextNode), typeof(OWVoiceMod), nameof(DisplayTextNode));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.ClearNomaiText), typeof(OWVoiceMod), nameof(ClearNomaiText));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.OnUnequipTool), typeof(OWVoiceMod), nameof(OnUnequipTool));

            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        public static void RegisterAssets(string assetsFolder)
        {
            foreach (string assetPath in Directory.EnumerateFiles(assetsFolder, "credits.xml", SearchOption.AllDirectories))
            {
                creditsAssetPath = assetPath;
                break;
            }

            foreach (string assetPath in Directory.EnumerateFiles(assetsFolder, "*.wav", SearchOption.AllDirectories)
                         .Concat(Directory.EnumerateFiles(assetsFolder, "*.mp3", SearchOption.AllDirectories))
                         .Concat(Directory.EnumerateFiles(assetsFolder, "*.ogg", SearchOption.AllDirectories)))
            {
                // Conjoins audio files of the same content with different names using &
                foreach (string assetName in Path.GetFileNameWithoutExtension(assetPath).Split('&'))
                {
                    assetPaths[assetName] = assetPath;
                }
            }
        }

        public override object GetApi()
        {
            return new VoiceModApi();
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
                try { creditsAsset.xml = new TextAsset(File.ReadAllText(creditsAssetPath)); }
                catch { ModHelper.Console.WriteLine("Credits file not found!", MessageType.Error); }
            }
            else if (loadScene is OWScene.SolarSystem or OWScene.EyeOfTheUniverse)
            {
                // Gives time for Start to run
                ModHelper.Events.Unity.FireOnNextUpdate(() =>
                {
                    audioSource = Locator.GetPlayerBody().gameObject.AddComponent<AudioSource>();
                    audioSource.volume = volume;
                    audioSource.outputAudioMixerGroup = Locator.GetAudioMixer().GetAudioMixerGroup(OWAudioMixer.TrackName.Environment);
                    // Placed on the Environment track to get environmental effects like reverb

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

        private static bool GetDisplayStringList(DialogueText __instance, ref List<string> __result)
        {
            // Ensures that the correct ID is used if dialogue uses randomization (ex. Gabbro intro lines)
            if (__instance._randomize)
            {
                randomDialogueNum = Random.Range(0, __instance._listTextBlocks.Count);
                __result = __instance._listTextBlocks[randomDialogueNum].listPageText;
                return false;
            }

            randomDialogueNum = -1;
            return true;
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

            string currentAssetName = xmlCharacterDialogueAsset.name + nodeName;
            currentAssetName += randomDialogueNum != -1 ? randomDialogueNum : pageNum;

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
            if (assetPaths.TryGetValue(assetName, out string assetPath))
            {
                audioSource.clip = GetAudio(assetPath);
                if (volume > 0 && audioSource.clip != null) audioSource.Play();
            }
            else
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

        private static AudioClip GetAudio(string path)
        {
            // Modified from https://github.com/amazingalek/owml/blob/master/src/OWML.ModHelper.Assets/ModAssets.cs#L99-L110
            using var reader = new AudioFileReader(path);
            var sampleCount = (int)(reader.Length * 8 / reader.WaveFormat.BitsPerSample);
            var outputSamples = new float[sampleCount];
            reader.Read(outputSamples, 0, sampleCount);
            var clip = AudioClip.Create(path, sampleCount / reader.WaveFormat.Channels, reader.WaveFormat.Channels, reader.WaveFormat.SampleRate, false);
            clip.SetData(outputSamples, 0);
            return clip;
        }
    }

    public class VoiceModApi
    {
        public void RegisterAssets(string assetsFolder)
        {
            OWVoiceMod.RegisterAssets(assetsFolder);
        }
    }
}