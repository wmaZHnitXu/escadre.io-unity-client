using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TokenResponseDto
{
    public string AccessToken { get; set; }
    public System.DateTime AccessTokenExpiration { get; set; }
    public string? NewRefreshToken { get; set; }

    public override string ToString()
    {
        return $"AccessToken: {AccessToken}, Expires: {AccessTokenExpiration}, NewRefreshToken: {NewRefreshToken ?? "null"}";
    }
}
