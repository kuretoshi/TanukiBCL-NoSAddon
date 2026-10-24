using System;
using System.Collections.Generic;
using AmongUs.Data;
using AmongUs.Data.Player;
using Assets.InnerNet;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula;
using Virial.Attributes;
using Virial.Runtime;

namespace TanukiBCL.Roles;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class AddonAnnouncements
{
    // Keep both identifiers stable so reopening the list does not duplicate news
    // or mark an already-read announcement as new on every launch.
    private const int DyingVoiceNumber = 267101801;
    private const string DyingVoiceId = "TanukiBCLNoSAddon.DyingVoiceMessage";
    private const int InterviewerNumber = 267101901;
    private const string InterviewerId = "TanukiBCLNoSAddon.Interviewer";
    private const int TitleLogoNumber = 267102001;
    private const string TitleLogoId = "TanukiBCLNoSAddon.TitleLogo";
    private const int VoiceJammingerNumber = 267102002;
    private const string VoiceJammingerId = "TanukiBCLNoSAddon.VoiceJamminger";
    private const int HeroesNGNumber = 267102401;
    private const string HeroesNGId = "TanukiBCLNoSAddon.HeroesNG";

    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        new Harmony("tanuki.bcl.roles.announcements").Patch(
            AccessTools.Method(typeof(PlayerAnnouncementData), nameof(PlayerAnnouncementData.SetAnnouncements)),
            prefix: new HarmonyMethod(typeof(AddonAnnouncements), nameof(AddNews)) { priority = Priority.First });
    }

    public static void AddNews([HarmonyArgument(0)] ref Il2CppReferenceArray<Announcement> announcements)
    {
        var news = new List<Announcement>();
        foreach (var item in announcements)
        {
            if (item.Number == DyingVoiceNumber && item.Id == DyingVoiceId) continue;
            if (item.Number == InterviewerNumber && item.Id == InterviewerId) continue;
            if (item.Number == TitleLogoNumber && item.Id == TitleLogoId) continue;
            if (item.Number == VoiceJammingerNumber && item.Id == VoiceJammingerId) continue;
            if (item.Number == HeroesNGNumber && item.Id == HeroesNGId) continue;
            news.Add(item);
        }
        news.Add(new Announcement {
            Number = DyingVoiceNumber,
            Id = DyingVoiceId,
            Title = Virial.NebulaAPI.Language.Translate("tanuki.news.dyingVoice.title"),
            ShortTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.dyingVoice.shortTitle"),
            SubTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.dyingVoice.subtitle"),
            Text = Virial.NebulaAPI.Language.Translate("tanuki.news.dyingVoice.body"),
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Date = "2026-10-18T00:00:00Z"
        });
        news.Add(new Announcement {
            Number = InterviewerNumber,
            Id = InterviewerId,
            Title = Virial.NebulaAPI.Language.Translate("tanuki.news.interviewer.title"),
            ShortTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.interviewer.shortTitle"),
            SubTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.interviewer.subtitle"),
            Text = Virial.NebulaAPI.Language.Translate("tanuki.news.interviewer.body"),
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Date = "2026-10-19T00:00:00Z"
        });
        news.Add(new Announcement {
            Number = TitleLogoNumber,
            Id = TitleLogoId,
            Title = Virial.NebulaAPI.Language.Translate("tanuki.news.titleLogo.title"),
            ShortTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.titleLogo.shortTitle"),
            SubTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.titleLogo.subtitle"),
            Text = Virial.NebulaAPI.Language.Translate("tanuki.news.titleLogo.body"),
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Date = "2026-10-20T00:00:00Z"
        });
        news.Add(new Announcement {
            Number = VoiceJammingerNumber,
            Id = VoiceJammingerId,
            Title = Virial.NebulaAPI.Language.Translate("tanuki.news.voiceJamminger.title"),
            ShortTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.voiceJamminger.shortTitle"),
            SubTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.voiceJamminger.subtitle"),
            Text = Virial.NebulaAPI.Language.Translate("tanuki.news.voiceJamminger.body"),
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Date = "2026-10-20T00:00:01Z"
        });
        news.Add(new Announcement {
            Number = HeroesNGNumber,
            Id = HeroesNGId,
            Title = Virial.NebulaAPI.Language.Translate("tanuki.news.heroesNG.title"),
            ShortTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.heroesNG.shortTitle"),
            SubTitle = Virial.NebulaAPI.Language.Translate("tanuki.news.heroesNG.subtitle"),
            Text = Virial.NebulaAPI.Language.Translate("tanuki.news.heroesNG.body"),
            Language = (uint)DataManager.Settings.Language.CurrentLanguage,
            Date = "2026-10-24T00:00:00Z"
        });
        news.Sort((left, right) => string.Compare(right.Date, left.Date, StringComparison.Ordinal));
        announcements = news.ToArray();
    }
}
