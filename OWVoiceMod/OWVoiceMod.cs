using OWML.ModHelper;
using OWML.Common;
using OWML.Utils;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace OWVoiceMod
{
    public class OWVoiceMod : ModBehaviour
    {
        private static IDictionary<string, AssetBundle> assetBundles = new Dictionary<string, AssetBundle>();
        private static GameObject player;
        private static AudioSource audioSource;
        private static NomaiTranslatorProp nomaiTranslatorProp;
        private static TextAsset xmlCharacterDialogueAsset;
        private static string characterName;
        private static string currentTextName;
        private static string currentBundleName;
        private static string oldTextName = null;
        private static string bundleToReload = null;
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
            foreach (string assetFileName in Directory.EnumerateFiles(ModHelper.Manifest.ModFolderPath))
            {
                string assetFileNameFormatted = Path.GetFileName(assetFileName);
                if (Path.GetExtension(assetFileNameFormatted) == string.Empty)
                {
                    assetBundles.Add(char.ToUpper(assetFileNameFormatted[0]) + assetFileNameFormatted.Substring(1), ModHelper.Assets.LoadBundle(assetFileNameFormatted));
                }
            }

            if (splashSkip)
            {
                //Skip splash screen (from vesper's half life mod)
                TitleScreenAnimation titleScreenAnimation = FindObjectOfType<TitleScreenAnimation>();
                TypeExtensions.SetValue(titleScreenAnimation, "_fadeDuration", 0);
                TypeExtensions.SetValue(titleScreenAnimation, "_gamepadSplash", false);
                TypeExtensions.SetValue(titleScreenAnimation, "_introPan", false);
                TypeExtensions.Invoke(titleScreenAnimation, "FadeInTitleLogo", new object[0]);
                TitleAnimationController titleAnimationController = FindObjectOfType<TitleAnimationController>();
                TypeExtensions.SetValue(titleAnimationController, "_logoFadeDelay", 0.001f);
                TypeExtensions.SetValue(titleAnimationController, "_logoFadeDuration", 0.001f);
                TypeExtensions.SetValue(titleAnimationController, "_optionsFadeDelay", 0.001f);
                TypeExtensions.SetValue(titleAnimationController, "_optionsFadeDuration", 0.001f);
                TypeExtensions.SetValue(titleAnimationController, "_optionsFadeSpacing", 0.001f);
            }

            ModHelper.HarmonyHelper.AddPrefix<CharacterDialogueTree>("StartConversation", typeof(OWVoiceMod), nameof(OWVoiceMod.StartConversation));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("DisplayTextNode", typeof(OWVoiceMod), nameof(OWVoiceMod.DisplayTextNode));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("ClearNomaiText", typeof(OWVoiceMod), nameof(OWVoiceMod.ClearNomaiText));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("OnUnequipTool", typeof(OWVoiceMod), nameof(OWVoiceMod.OnUnequipTool));

            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        void Update()
        {
            if (bundleToReload != null)
            {
                assetBundles[bundleToReload] = ModHelper.Assets.LoadBundle(bundleToReload.ToLower());
                bundleToReload = null;
            }
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

        private void OnCompleteSceneLoad(OWScene orignalScene, OWScene loadScene)
        {
            if (loadScene != OWScene.SolarSystem) return;

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
            if (characterName != "NOTE") audioSource = __instance.gameObject.AddComponent<AudioSource>(); //may not work without OWAudioSource?
        }

        private void OnAdvancePage(string nodeName, int pageNum)
        {
            if (!assetBundles.ContainsKey(xmlCharacterDialogueAsset.name)) return;
            if (!conversations && characterName != "NOTE" && characterName != "RECORDING") return;
            if (!hearthianRecordings && characterName == "RECORDING") return;
            if (!paperNotes && characterName == "NOTE") return; 
            audioSource.Stop();
            string currentAssetName = xmlCharacterDialogueAsset.name + nodeName + pageNum.ToString();
            foreach (string characterModdedAudioName in assetBundles[xmlCharacterDialogueAsset.name].GetAllAssetNames()) 
            {
                string characterModdedAudioNameFormatted = Path.GetFileNameWithoutExtension(characterModdedAudioName);
                if (characterModdedAudioNameFormatted.Split('+').Any(x => x == currentAssetName.ToLower()))
                {
                    audioSource.clip = assetBundles[xmlCharacterDialogueAsset.name].LoadAsset<AudioClip>(characterModdedAudioNameFormatted);
                    if (volume > 0) audioSource.Play();
                    break;
                }
            }
        }

        private void OnEndConversation()
        {
            audioSource = player.GetComponent<AudioSource>();
            if (!assetBundles.ContainsKey(xmlCharacterDialogueAsset.name)) return;
            audioSource.Stop();
            bundleToReload = xmlCharacterDialogueAsset.name;
            assetBundles[xmlCharacterDialogueAsset.name].Unload(true);
        }

        private static void DisplayTextNode()
        {
            NomaiText nomaiText = nomaiTranslatorProp._nomaiTextComponent;
            int currentTextID = nomaiTranslatorProp._currentTextID;
            if (nomaiText is NomaiComputer || nomaiText is NomaiVesselComputer)
            {
                if (!nomaiComputers) return;
                if (nomaiText.gameObject.TryGetComponent<NomaiWarpComputerLogger>(out NomaiWarpComputerLogger nomaiWarpComputerLogger)) currentBundleName = "NomaiWarpComputer";  
                else currentBundleName = nomaiText._nomaiTextAsset.name;
                currentTextName = currentBundleName + currentTextID.ToString();
            }
            else if (nomaiText is GhostWallText)
            {
                if (!owlkWriting) return;
                currentBundleName = "OwlkStatic";
                currentTextName = "OwlkStatic";
            }
            else
            {
                if (!nomaiScrolls && nomaiText is NomaiWallText) return;
                if (!nomaiRecordings && !(nomaiText is NomaiWallText)) return;
                currentBundleName = nomaiText._nomaiTextAsset.name;
                currentTextName = currentBundleName + currentTextID.ToString();
            }

            if (!assetBundles.ContainsKey(currentBundleName)) return;

            if (currentTextName != oldTextName)
            {
                audioSource.Stop();
                if (nomaiText.IsTranslated(currentTextID))
                {
                    foreach (string characterModdedAudioName in assetBundles[currentBundleName].GetAllAssetNames())
                    {
                        string characterModdedAudioNameFormatted = Path.GetFileNameWithoutExtension(characterModdedAudioName);
                        if (characterModdedAudioNameFormatted.Split('+').Any(x => x == currentTextName.ToLower()))
                        {
                            audioSource.clip = assetBundles[currentBundleName].LoadAsset<AudioClip>(characterModdedAudioNameFormatted);
                            if (volume > 0) audioSource.Play();
                            break;
                        }
                    }
                    if (!(nomaiText is GhostWallText)) oldTextName = currentTextName;
                } else
                {
                    oldTextName = null;
                }
            }
        }

        private static void ClearNomaiText()
        {
            audioSource.Stop();
            oldTextName = null;
        }

        private static void OnUnequipTool()
        {
            if (!assetBundles.ContainsKey(currentBundleName)) return;
            audioSource.Stop();
            bundleToReload = currentBundleName;
            assetBundles[currentBundleName].Unload(true);
            oldTextName = null;
        }
    }
}