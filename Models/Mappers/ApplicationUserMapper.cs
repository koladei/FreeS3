using CradleSoft.DMS.Models;
using CradleSoft.DMS.Models.Dtos;
using Riok.Mapperly.Abstractions;


namespace CradleSoft.DMS.Models.Mappers
{
  [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
  public static partial class ApplicationUserMapper
  {
    [MapProperty(nameof(RegisterUserDto.Username), nameof(ApplicationUser.UserName))]
    public static partial ApplicationUser RegisterUserDtoToApplicationUser(RegisterUserDto dto);

    [MapProperty(nameof(ApplicationUser.UserName), nameof(RegisterUserDto.Username))]
    public static partial RegisterUserDto ApplicationUserDtoToRegisterUser(ApplicationUser user);
  }
}
