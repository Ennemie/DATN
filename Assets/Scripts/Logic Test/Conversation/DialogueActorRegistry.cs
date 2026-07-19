// Chức năng: Registry tĩnh để DialogueController tìm camera target mặc định của actor trong scene.
// Lý do cần registry: DialogueActorProfile là asset dùng lại, không nên trực tiếp giữ scene Transform.
using System.Collections.Generic;
using UnityEngine;

public static class DialogueActorRegistry
{
    private static readonly Dictionary<DialogueActorProfile, DialogueActorSceneBinding> bindingByProfile = new Dictionary<DialogueActorProfile, DialogueActorSceneBinding>();

    public static void Register(DialogueActorSceneBinding binding)
    {
        if (binding == null || binding.ActorProfile == null)
            return;

        bindingByProfile[binding.ActorProfile] = binding;
    }

    public static void Unregister(DialogueActorSceneBinding binding)
    {
        if (binding == null || binding.ActorProfile == null)
            return;

        if (bindingByProfile.TryGetValue(binding.ActorProfile, out DialogueActorSceneBinding current) && current == binding)
            bindingByProfile.Remove(binding.ActorProfile);
    }

    public static Transform GetDefaultCameraTarget(DialogueActorProfile profile)
    {
        if (profile == null)
            return null;

        if (bindingByProfile.TryGetValue(profile, out DialogueActorSceneBinding binding) && binding != null)
            return binding.DefaultCameraTarget;

        // Fallback: quét scene nếu binding chưa kịp register hoặc object đang được bật muộn.
        DialogueActorSceneBinding[] allBindings = Resources.FindObjectsOfTypeAll<DialogueActorSceneBinding>();
        for (int i = 0; i < allBindings.Length; i++)
        {
            DialogueActorSceneBinding candidate = allBindings[i];
            if (candidate != null && candidate.ActorProfile == profile)
            {
                Register(candidate);
                return candidate.DefaultCameraTarget;
            }
        }

        return null;
    }
}
