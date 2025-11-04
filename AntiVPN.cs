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
                if (!player.IsValid || player.IsBot || player.IsHLTV || player.AuthorizedSteamID == null) continue;
                CheckPlayerIp(player);
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
            CheckPlayerIp(player);
        });

        return HookResult.Continue;
    }
    
    private void CheckPlayerIp(CCSPlayerController player)
    {
        if (!player.IsValid) return;
        var ipAddress = player.IpAddress?.Split(":")[0] ?? "error";
        if (ipAddress.Equals("error")) return;
        
        var steamid = player.AuthorizedSteamID!.SteamId64.ToString();

        var admin = AdminManager.GetPlayerAdminData(new SteamID(player.SteamID));
        if (admin?.GetAllFlags().Contains("@css/reservation") ?? false) return;

        Task.Run(async () =>
        {
            var isVpn = await IsIpVpn(ipAddress);
            await Server.NextFrameAsync(() =>
            {
                AddTimer(5f, () =>
                {
                    if (!player.IsValid  || !isVpn) return;
                    
                    player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_REJECTED_BY_GAME);
                    Logger.LogInformation($"Player {player.PlayerName} ({steamid}) ({ipAddress}) has been kicked. (VPN Usage)");
                }, TimerFlags.STOP_ON_MAPCHANGE);
            });

        });
    }

    private async Task <bool>IsIpVpn(string ipAddress)
    {
        using var client = new HttpClient();
        var requestUrl = $"https://blackbox.ipinfo.app/lookup/{ipAddress}";

        var response = await client.GetAsync(requestUrl);
        if (!response.IsSuccessStatusCode) return false;
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        return jsonResponse == "Y";
    }
}
