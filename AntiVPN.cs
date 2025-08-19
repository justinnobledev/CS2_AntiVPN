using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Microsoft.Extensions.Logging;

namespace AntiVPN;

public class AntiVPN : BasePlugin
{
    public override string ModuleName => "Anti VPN";
    public override string ModuleAuthor => "Nocky & Retro";
    public override string ModuleVersion => "1.1";

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || player.AuthorizedSteamID == null) continue;
                CheckPlayerIP(player);
            }
        }
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV || player.AuthorizedSteamID == null) 
            return HookResult.Continue;

        AddTimer(1f, () =>
        {
            CheckPlayerIP(player);
        });

        return HookResult.Continue;
    }
    
    public void CheckPlayerIP(CCSPlayerController player)
    {
        string ipAddress = player!.IpAddress!.Split(":")[0];
        string steamid = player!.AuthorizedSteamID!.SteamId64.ToString();

        var admin = AdminManager.GetPlayerAdminData(new SteamID(player.SteamID));
        if (admin?.GetAllFlags().Contains("@css/reservation") ?? false) return;

        Task.Run(async () =>
        {
            var isVPN = await IsIpVPN(ipAddress);
            Server.NextFrame(() =>
            {
                AddTimer(5f, () =>
                {
                    if (!player.IsValid) return;
                if (isVPN)
                {
                    player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_REJECTED_BY_GAME);
                    Logger.LogInformation($"Player {player.PlayerName} ({steamid}) ({ipAddress}) has been kicked. (VPN Usage)");
                }
                }, TimerFlags.STOP_ON_MAPCHANGE);
            });

        });
    }
    static async Task <bool>IsIpVPN(string ipAddress)
    {
        using (var client = new HttpClient())
        {
            string requestURL = $"https://blackbox.ipinfo.app/lookup/{ipAddress}";

            HttpResponseMessage response = await client.GetAsync(requestURL);
            if (response.IsSuccessStatusCode)
            {
                string jsonResponse = await response.Content.ReadAsStringAsync();
                if(jsonResponse == "Y"){
                    return true;
                }
                else{
                    return false;
                }
            }
            return false;
        }
    }
}
