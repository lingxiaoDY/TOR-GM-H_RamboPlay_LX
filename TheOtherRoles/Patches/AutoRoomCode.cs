using HarmonyLib;
using UnityEngine;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
    public class AutoRoomCode
    {
        public static void Postfix(GameStartManager __instance)
        {
            __instance.AddChat(PlayerControl.LocalPlayer, $"房间代码 {InnerNet.GameCode.IntToGameName(AmongUsClient.Instance.GameId)} 已复制");
            GUIUtility.systemCopyBuffer = InnerNet.GameCode.IntToGameName(AmongUsClient.Instance.GameId);
        }
    }
}