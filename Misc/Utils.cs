using System.Linq;
using LSPD_First_Response.Mod.API;
using Rage;

namespace MTFO.Misc
{
    public class Utils
    {
        internal static bool IsDriverInPursuit(Ped p)
        {
            return Functions.GetActivePursuit() != null && Functions.GetPursuitPeds(Functions.GetActivePursuit()).Contains(p);
        }
    }
}