using HarmonyLib;
using UnityEngine;
using Hazel;

namespace TheOtherRoles.Patches
{
    //チャットバグ対策
    [HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
    public static class ChatControllerAwakePatch
    {
        public static void Prefix()
        {
            SaveManager.chatModeType = 1;
            SaveManager.isGuest = false;
        }
        public static void Postfix(ChatController __instance)
        {
            SaveManager.chatModeType = 1;
            SaveManager.isGuest = false;
            if (Input.GetKeyDown(KeyCode.F1))
            {
                if (!__instance.isActiveAndEnabled) return;
                __instance.SetVisible(false);
                new LateTask(() =>
                {
                    __instance.SetVisible(true);
                }, 0f, "AntiChatBag");
            }
        }
    }
}