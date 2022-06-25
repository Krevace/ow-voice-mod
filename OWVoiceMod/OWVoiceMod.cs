using NAudio.Wave;
using OWML.ModHelper;
using OWML.Common;
using OWML.Utils;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml;

namespace OWVoiceMod
{
    public class OWVoiceMod : ModBehaviour
    {
        private new static IModHelper ModHelper;
        private static readonly Dictionary<string, string> assetPaths = new();
        private static AudioSource audioSource;
        private static NomaiTranslatorProp nomaiTranslatorProp;

        private static TextAsset xmlCharacterDialogueAsset;
        private static string characterName;
        private static string currentTextName;
        private static string oldTextName;
        private static int randomDialogueNum;

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
                         .Concat(Directory.EnumerateFiles(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets"), "*.mp3", SearchOption.AllDirectories))
                         .Concat(Directory.EnumerateFiles(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets"), "*.ogg", SearchOption.AllDirectories)))
            {
                foreach (string assetName in Path.GetFileNameWithoutExtension(assetPath).Split('+'))
                {
                    assetPaths[assetName] = assetPath;
                }
            }

            if (splashSkip)
            {
                // copied from https://github.com/Vesper-Works/OuterWildsOnline/blob/master/OuterWildsOnline/ConnectionController.cs#L106-L119
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

            ModHelper.HarmonyHelper.AddPrefix<TextTranslation>(nameof(TextTranslation.SetLanguage), typeof(OWVoiceMod), nameof(SetLanguage));
            ModHelper.HarmonyHelper.AddPrefix<DialogueText>(nameof(DialogueText.GetDisplayStringList), typeof(OWVoiceMod), nameof(GetDisplayStringList));
            ModHelper.HarmonyHelper.AddPrefix<CharacterDialogueTree>(nameof(CharacterDialogueTree.StartConversation), typeof(OWVoiceMod), nameof(StartConversation));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.DisplayTextNode), typeof(OWVoiceMod), nameof(DisplayTextNode));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.ClearNomaiText), typeof(OWVoiceMod), nameof(ClearNomaiText));
            ModHelper.HarmonyHelper.AddPrefix<NomaiTranslatorProp>(nameof(NomaiTranslatorProp.OnUnequipTool), typeof(OWVoiceMod), nameof(OnUnequipTool));

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

        private static bool SetLanguage()
        {
            try
            {
                TextAsset translation = new(File.ReadAllText(Path.Combine(ModHelper.Manifest.ModFolderPath, "assets", "Translation.bytes")));
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
                textTranslation.m_table = new TextTranslation.TranslationTable(translationTable_XML);
            }
            catch { ModHelper.Console.WriteLine("Translation file not found, game needs to be reloaded!", MessageType.Error); }
            return false;
        }

        private static bool GetDisplayStringList(DialogueText __instance, ref List<string> __result)
        {
            if (__instance._randomize)
            {
                //maybe add (list.count > 0) test or blocksatisfiesconditions() test idk
                randomDialogueNum = Random.Range(0, __instance._listTextBlocks.Count);
                __result = __instance._listTextBlocks[randomDialogueNum].listPageText;
                return false;
            }
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
            currentAssetName += nodeName == "GabbroMain" ? randomDialogueNum : pageNum;

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
            // modified from https://github.com/amazingalek/owml/blob/master/src/OWML.ModHelper.Assets/ModAssets.cs#L99-L110
            using var reader = new AudioFileReader(path);
            var sampleCount = (int)(reader.Length * 8 / reader.WaveFormat.BitsPerSample);
            var outputSamples = new float[sampleCount];
            reader.Read(outputSamples, 0, sampleCount);
            var clip = AudioClip.Create(path, sampleCount / reader.WaveFormat.Channels, reader.WaveFormat.Channels, reader.WaveFormat.SampleRate, false);
            clip.SetData(outputSamples, 0);
            return clip;
        }
    }
}