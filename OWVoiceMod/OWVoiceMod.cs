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
        private static NomaiTranslatorProp nomaiTranslatorProp;
        private static TextAsset xmlCharacterDialogueAsset;
        private static string currentTextName;
        private static string currentBundleName;
        private static string oldTextName = null;
        private static string bundleToReload = null;

        private void Start()
        {
            foreach (string assetFileName in Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
            {
                string assetFileNameFormatted = assetFileName.Split('\\')[9];
                if (!assetFileNameFormatted.Contains("manifest") && !assetFileNameFormatted.Contains("json") && !assetFileNameFormatted.Contains("dll") && !assetFileNameFormatted.Contains("pdb"))
                {
                    assetBundles.Add(char.ToUpper(assetFileNameFormatted[0]) + assetFileNameFormatted.Substring(1), ModHelper.Assets.LoadBundle(assetFileNameFormatted));
                }
            }

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

            ModHelper.HarmonyHelper.AddPrefix<CharacterDialogueTree>("StartConversation", typeof(OWVoiceMod), nameof(OWVoiceMod.StartConversation));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("DisplayTextNode", typeof(OWVoiceMod), nameof(OWVoiceMod.DisplayTextNode));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("ClearNomaiText", typeof(OWVoiceMod), nameof(OWVoiceMod.ClearNomaiText));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>("OnUnequipTool", typeof(OWVoiceMod), nameof(OWVoiceMod.OnUnequipTool));

            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            ModHelper.Console.WriteLine($"{nameof(OWVoiceMod)} is loaded!", MessageType.Success);
        }

        private void OnCompleteSceneLoad(OWScene orignalScene, OWScene loadScene)
        {
            if (loadScene != OWScene.SolarSystem) return;

            player = GameObject.Find("Player_Body");
            player.AddComponent<AudioSource>();

            CharacterDialogueTree[] characterDialogueTree = FindObjectsOfType<CharacterDialogueTree>();
            foreach (CharacterDialogueTree childCharacterDialogueTree in characterDialogueTree)
            {
                childCharacterDialogueTree.OnAdvancePage += OnAdvancePage;
                childCharacterDialogueTree.OnEndConversation += OnEndConversation;
            }

            NomaiText[] nomaiText = FindObjectsOfType<NomaiText>();

            nomaiTranslatorProp = FindObjectOfType<NomaiTranslatorProp>();
        }

        void Update()
        {
            if (bundleToReload != null)
            {
                assetBundles[bundleToReload] = ModHelper.Assets.LoadBundle(bundleToReload);
                bundleToReload = null;
            }
        }

        private static void StartConversation(ref TextAsset ____xmlCharacterDialogueAsset)
        {
            //make new audio source here
            xmlCharacterDialogueAsset = ____xmlCharacterDialogueAsset;
        }

        private void OnAdvancePage(string nodeName, int pageNum)
        {
            player.GetComponent<AudioSource>().Stop();
            string currentAssetName = xmlCharacterDialogueAsset.name + nodeName + pageNum.ToString();
            foreach (string characterModdedAudioName in assetBundles[xmlCharacterDialogueAsset.name].GetAllAssetNames()) 
            {
                string characterModdedAudioNameFormatted = characterModdedAudioName.Split('/')[3].TrimEnd('3', 'p', 'm', '.');
                if (characterModdedAudioNameFormatted.Split('+').Any(x => x == currentAssetName.ToLower()))
                {
                    player.GetComponent<AudioSource>().clip = assetBundles[xmlCharacterDialogueAsset.name].LoadAsset<AudioClip>(characterModdedAudioNameFormatted);
                    player.GetComponent<AudioSource>().Play();
                    break;
                }
            }
        }

        private void OnEndConversation()
        {
            //delete audio source here
            player.GetComponent<AudioSource>().Stop();
            bundleToReload = xmlCharacterDialogueAsset.name;
            assetBundles[xmlCharacterDialogueAsset.name].Unload(true);
        }

        private static void DisplayTextNode()
        {
            NomaiText nomaiText = nomaiTranslatorProp._nomaiTextComponent;
            int currentTextID = nomaiTranslatorProp._currentTextID;
            if (nomaiText is NomaiComputer)
            {
                currentBundleName = "NomaiWarpComputer";
                currentTextName = currentBundleName + currentTextID.ToString();
            }
            else if (nomaiText is GhostWallText)
            {
                currentBundleName = "OwlkStatic";
                currentTextName = "OwlkStatic";
            }
            else
            {
                currentBundleName = nomaiText._nomaiTextAsset.name;
                currentTextName = currentBundleName + currentTextID.ToString();
            }

            if (currentTextName != oldTextName)
            {
                player.GetComponent<AudioSource>().Stop();
                if (nomaiText.IsTranslated(currentTextID))
                {
                    foreach (string characterModdedAudioName in assetBundles[currentBundleName].GetAllAssetNames())
                    {
                        string characterModdedAudioNameFormatted = characterModdedAudioName.Split('/')[3].TrimEnd('3', 'p', 'm', '.');
                        if (characterModdedAudioNameFormatted.Split('+').Any(x => x == currentTextName.ToLower()))
                        {
                            player.GetComponent<AudioSource>().clip = assetBundles[currentBundleName].LoadAsset<AudioClip>(characterModdedAudioNameFormatted);
                            player.GetComponent<AudioSource>().Play();
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
            player.GetComponent<AudioSource>().Stop();
            oldTextName = null;
        }

        private static void OnUnequipTool()
        {
            player.GetComponent<AudioSource>().Stop();
            bundleToReload = currentBundleName;
            assetBundles[currentBundleName].Unload(true);
            oldTextName = null;
        }
    }
}