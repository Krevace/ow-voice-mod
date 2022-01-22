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

        private void Start()
        {
            assetBundle = ModHelper.Assets.LoadBundle("bundleoassets");

            //Skip splash screen (from vesper's half life mod)
            ModHelper.Console.WriteLine("Skipping splash screen...");
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
            ModHelper.Console.WriteLine("Done!");

            ModHelper.HarmonyHelper.AddPrefix<TextTranslation>("SetLanguage", typeof(OWVoiceMod), nameof(OWVoiceMod.SetLanguage));

            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

            ModHelper.Console.WriteLine($"{nameof(OWVoiceMod)} is loaded!", MessageType.Success);
        }

        private static bool SetLanguage()
        {
            TextAsset translation = assetBundle.LoadAsset<TextAsset>("Translation");
            string xml = OWUtilities.RemoveByteOrderMark(translation);
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(xml);
            XmlNode xmlNode = xmlDocument.SelectSingleNode("TranslationTable_XML");
            XmlNodeList xmlNodeList = xmlNode.SelectNodes("entry");
            TextTranslation.TranslationTable_XML translationTable_XML = new TextTranslation.TranslationTable_XML();
            foreach (object obj in xmlNodeList)
            {
                XmlNode xmlNode2 = (XmlNode)obj;
                translationTable_XML.table.Add(new TextTranslation.TranslationTableEntry(xmlNode2.SelectSingleNode("key").InnerText, xmlNode2.SelectSingleNode("value").InnerText));
            }
            foreach (object obj2 in xmlNode.SelectSingleNode("table_shipLog").SelectNodes("TranslationTableEntry"))
            {
                XmlNode xmlNode3 = (XmlNode)obj2;
                translationTable_XML.table_shipLog.Add(new TextTranslation.TranslationTableEntry(xmlNode3.SelectSingleNode("key").InnerText, xmlNode3.SelectSingleNode("value").InnerText));
            }
            foreach (object obj3 in xmlNode.SelectSingleNode("table_ui").SelectNodes("TranslationTableEntryUI"))
            {
                XmlNode xmlNode4 = (XmlNode)obj3;
                translationTable_XML.table_ui.Add(new TextTranslation.TranslationTableEntryUI(int.Parse(xmlNode4.SelectSingleNode("key").InnerText), xmlNode4.SelectSingleNode("value").InnerText));
            }
            TextTranslation textTranslation = FindObjectOfType<TextTranslation>();
            TypeExtensions.SetValue(textTranslation, "m_table", new TextTranslation.TranslationTable(translationTable_XML));
            return false;
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
                foreach (TextAsset characterModdedDialogueFile in assetBundle.LoadAllAssets<TextAsset>())
                {
                    if (characterModdedDialogueFile.name == TypeExtensions.GetValue<TextAsset>(childCharacterDialogueTree, "_xmlCharacterDialogueAsset").name)
                    {
                        TypeExtensions.SetValue(childCharacterDialogueTree, "_xmlCharacterDialogueAsset", characterModdedDialogueFile);
                    }
                }
            } 
        }

        private void OnAdvancePage(string nodeName, int pageNum)
        {
            player.GetComponent<AudioSource>().Stop();
            foreach (AudioClip characterModdedAudioFile in assetBundle.LoadAllAssets<AudioClip>())
            {
                if (characterModdedAudioFile.name == nodeName + pageNum.ToString())
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

        //make everything work for nomai walls as well
    }
}
