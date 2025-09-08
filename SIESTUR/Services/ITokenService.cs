// Services/ITokenService.cs
using Siestur.Models;

namespace Siestur.Services;

public interface ITokenService
{
    (string token, DateTime expiresAt) BuildToken(User user);
}
