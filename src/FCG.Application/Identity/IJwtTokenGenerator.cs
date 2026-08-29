using FCG.Domain.Identity;

namespace FCG.Application.Identity;

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}
