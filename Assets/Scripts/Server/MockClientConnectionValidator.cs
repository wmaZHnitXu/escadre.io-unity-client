// File: Scripts/Server/Session/MockClientConnectionValidator.cs 
using System.IO;
using Core.Session;
using Core.Logging; 

namespace ServerSpecific.Session
{
    public class MockClientConnectionValidator : IClientConnectionValidator
    {
        public ClientIdentity? ValidateTokenAndGetIdentity(BinaryReader payload)
        {
            string mockJwtString;
            try
            {
                // Client is expected to send the JWT as a string in the payload
                mockJwtString = payload.ReadString();
            }
            catch (EndOfStreamException)
            {
                Logger.LogWarning("[MockClientConnectionValidator] Connection request payload was empty or too short. Expected JWT string.");
                return null;
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[MockClientConnectionValidator] Error reading JWT from payload: {ex.Message}");
                return null;
            }

            // --- Mock JWT "Parsing" ---
            // In a real scenario, you'd use a JWT library to parse and validate the token.
            // For this mock, we'll assume the string is in a format like:
            // "ID:123,Nick:PlayerOne,Admin:false,AuthType:Account"
            // Or simpler: "123;PlayerOne;false;Account"

            Logger.Log($"[MockClientConnectionValidator] Received mock JWT: '{mockJwtString}'");

            try
            {
                string[] parts = mockJwtString.Split(';');
                if (parts.Length < 4) 
                {
                    Logger.LogWarning($"[MockClientConnectionValidator] Mock JWT string format error. Expected 4 parts, got {parts.Length}. JWT: '{mockJwtString}'");
                    return null;
                }

                if (!int.TryParse(parts[0], out int clientId))
                {
                    Logger.LogWarning($"[MockClientConnectionValidator] Could not parse ClientId from mock JWT. Part: '{parts[0]}'");
                    return null;
                }

                string nickname = parts[1];

                if (!bool.TryParse(parts[2], out bool isAdmin))
                {
                    Logger.LogWarning($"[MockClientConnectionValidator] Could not parse IsAdmin from mock JWT. Part: '{parts[2]}'");
                    return null;
                }

                AuthTypeEnum authType;
                if (parts[3].Equals("Account", System.StringComparison.OrdinalIgnoreCase))
                {
                    authType = AuthTypeEnum.Account;
                }
                else if (parts[3].Equals("NoAccount", System.StringComparison.OrdinalIgnoreCase))
                {
                    authType = AuthTypeEnum.NoAccount;
                }
                else
                {
                    Logger.LogWarning($"[MockClientConnectionValidator] Unknown AuthType string in mock JWT: '{parts[3]}'. Defaulting to NoAccount.");
                    authType = AuthTypeEnum.NoAccount; // Or return null if strict
                }
                
                Logger.Log($"[MockClientConnectionValidator] Mock JWT parsed successfully. ClientID: {clientId}, Nick: {nickname}, Admin: {isAdmin}, AuthType: {authType}");
                return new ClientIdentity(clientId, nickname, isAdmin, authType);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[MockClientConnectionValidator] Exception during mock JWT parsing: {ex.Message}. JWT: '{mockJwtString}'");
                return null;
            }
        }
    }
}