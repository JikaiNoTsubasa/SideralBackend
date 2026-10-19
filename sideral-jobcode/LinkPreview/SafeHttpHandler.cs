using System.Net;
using System.Net.Sockets;

namespace sideral_jobcode.LinkPreview;

/// <summary>
/// Gestionnaire HTTP pour les requêtes vers des URL fournies par les utilisateurs (aperçus de liens) :
/// refuse de se connecter à une adresse privée, locale ou réservée (protection SSRF). La vérification est faite
/// sur l'adresse réellement connectée, ce qui couvre aussi les redirections et le « DNS rebinding ».
/// </summary>
public static class SafeHttpHandler
{
    public static SocketsHttpHandler Create() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        UseCookies = false,
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var address = addresses.FirstOrDefault(IsPublic)
                ?? throw new HttpRequestException($"Adresse non autorisée : {context.DnsEndPoint.Host}");

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    /// <summary>Adresse routable sur Internet (ni boucle locale, ni réseau privé, ni lien local, ni réservée).</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 10
                || b[0] == 0
                || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // CGNAT
                || (b[0] == 169 && b[1] == 254)                 // lien local (dont métadonnées cloud)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                || b[0] >= 224);                                // multicast et réservé
        }

        return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
            || address.IsIPv6UniqueLocal || address.Equals(IPAddress.IPv6Loopback));
    }
}
