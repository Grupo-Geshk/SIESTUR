// Services/IDayResetService.cs
using System.Threading.Tasks;

namespace Siestur.Services;

public interface IDayResetService
{
    Task<(int turnsArchived, int startDefault)> ResetTodayAsync();
}
