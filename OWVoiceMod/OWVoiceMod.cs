using System;
using System.Reflection;
using System.Xml;
using OWML.ModHelper;
using OWML.Common;
using OWML.Utils;
using UnityEngine;

namespace OWVoiceMod
{
    public class OWVoiceMod : ModBehaviour
    {
        private static AssetBundle assetBundle;
        private static GameObject player;
        private static NomaiTranslatorProp nomaiTranslatorProp;
        private static TextAsset xmlCharacterDialogueAsset;
        private static string currentTextName;
        private static string oldTextName = null;

        private void Start()
        {
            assetBundle = ModHelper.Assets.LoadBundle("bundleoassets");

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

        private static void StartConversation(ref TextAsset ____xmlCharacterDialogueAsset)
        {
            xmlCharacterDialogueAsset = ____xmlCharacterDialogueAsset;
        }

        private void OnAdvancePage(string nodeName, int pageNum)
        {
            player.GetComponent<AudioSource>().Stop();
            foreach (AudioClip characterModdedAudioFile in assetBundle.LoadAllAssets<AudioClip>())
            {
                if (characterModdedAudioFile.name == xmlCharacterDialogueAsset.name + nodeName + pageNum.ToString())
                {
                    player.GetComponent<AudioSource>().clip = characterModdedAudioFile;
                    player.GetComponent<AudioSource>().Play();
                }
            }
        }

        private void OnEndConversation()
        {
            player.GetComponent<AudioSource>().Stop();
        }

        private static void DisplayTextNode()
        {
            NomaiText nomaiText = TypeExtensions.GetValue<NomaiText>(nomaiTranslatorProp, "_nomaiTextComponent");
            int currentTextID = TypeExtensions.GetValue<int>(nomaiTranslatorProp, "_currentTextID");
            currentTextName = nomaiText._nomaiTextAsset.name + currentTextID.ToString();

            if (currentTextName != oldTextName)
            {
                player.GetComponent<AudioSource>().Stop();
                if (nomaiText.IsTranslated(currentTextID))
                {
                    foreach (AudioClip characterModdedAudioFile in assetBundle.LoadAllAssets<AudioClip>())
                    {
                        if (characterModdedAudioFile.name == currentTextName)
                        {
                            player.GetComponent<AudioSource>().clip = characterModdedAudioFile;
                            player.GetComponent<AudioSource>().Play();
                        }
                    }
                    oldTextName = currentTextName;
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
            oldTextName = null;
        }
    }
}
