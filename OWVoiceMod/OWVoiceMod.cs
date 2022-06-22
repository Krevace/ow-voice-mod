using OWML.ModHelper;
using OWML.Common;
using OWML.Utils;
using UnityEngine;
using System.IO;

namespace OWVoiceMod
{
    public class OWVoiceMod : ModBehaviour
    {
        private static IModAssets iModAssets;
        private static IModConsole iModConsole;
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
            iModAssets = ModHelper.Assets;
            iModConsole = ModHelper.Console;

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
            if (loadScene == OWScene.Credits_Fast || loadScene == OWScene.Credits_Final)
            {
                CreditsAsset creditsAsset = FindObjectOfType<Credits>()._creditsAsset;
                try { creditsAsset.xml = new TextAsset(File.ReadAllText(ModHelper.Manifest.ModFolderPath + "credits.bytes")); } 
                catch { iModConsole.WriteLine("Credits file not found!", MessageType.Error); }
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

        private void OnAdvancePage(string nodeName, int pageNum)
        {
            if (!conversations && characterName != "NOTE" && characterName != "RECORDING") return;
            if (!hearthianRecordings && characterName == "RECORDING") return;
            if (!paperNotes && characterName == "NOTE") return; 

            audioSource.Stop();
            if (audioSource.clip != null) audioSource.clip.UnloadAudioData();
            audioSource.clip = null;

            string currentAssetName = xmlCharacterDialogueAsset.name + nodeName + pageNum.ToString();
            try { audioSource.clip = ModHelper.Assets.GetAudio(currentAssetName + ".wav"); } catch { }
            if (volume > 0 && audioSource.clip != null) audioSource.Play();
        }

        private void OnEndConversation()
        {
            audioSource.Stop();
            if (audioSource.clip != null) audioSource.clip.UnloadAudioData();
            audioSource.clip = null;
        }

        private static void DisplayTextNode()
        {
            NomaiText nomaiText = nomaiTranslatorProp._nomaiTextComponent;
            int currentTextID = nomaiTranslatorProp._currentTextID;
            if (nomaiText is NomaiComputer || nomaiText is NomaiVesselComputer)
            {
                if (!nomaiComputers) return;
                if (nomaiText.gameObject.TryGetComponent<NomaiWarpComputerLogger>(out NomaiWarpComputerLogger nomaiWarpComputerLogger)) currentAssetName = "NomaiWarpComputer";  
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
                if (!nomaiRecordings && !(nomaiText is NomaiWallText)) return;
                currentAssetName = nomaiText._nomaiTextAsset.name;
                currentTextName = currentAssetName + currentTextID.ToString();
            }

            if (currentTextName != oldTextName)
            {
                audioSource.Stop();
                if (audioSource.clip != null) audioSource.clip.UnloadAudioData();
                audioSource.clip = null;

                if (nomaiText.IsTranslated(currentTextID))
                {
                    try { audioSource.clip = iModAssets.GetAudio(currentTextName + ".wav"); } catch { }
                    if (volume > 0 && audioSource.clip != null) audioSource.Play();

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
            if (audioSource.clip != null) audioSource.clip.UnloadAudioData();
            audioSource.clip = null;
            oldTextName = null;
        }

        private static void OnUnequipTool()
        {
            audioSource.Stop();
            if (audioSource.clip != null) audioSource.clip.UnloadAudioData();
            audioSource.clip = null;
            oldTextName = null;
        }
    }
}