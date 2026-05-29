namespace VTT.Network.HTTPAPI
{
    using System;
    using System.Collections.Generic;

    [APIMethodDocs(
        Name = "Authorize", 
        Tags = new string[] {"Anonimous"}, 
        Returns = new string[] {
            "200 - OK if authorization successful, with the body being the AUTH key to use for the current session.",
            "401 - Unauthorized if invalid.",
            "WS: { \"status\": STATUSCODE, \"auth\": \"AUTHKEY\" }"
        },

        Desc = "Sends an authorization request to the server."
    )]
    public class MethodAuthorize : APIMethod
    {
        public override string Identifier => "authorize";

        [APIMethodDocs(Name = "id", ValueKey = "%GUID%", Desc = "The GUID of the client, standard GUID format.")]
        private Guid _id;

        [APIMethodDocs(Name = "secret", ValueKey = "%B64SECRET%", Desc = "The base-64 encoded secret of the client.")]
        private byte[] _secret;

        public override void Act(HTTPAPIEndpoint api)
        {
            if (!api.IterateClientsForKV(this._id, this._secret, out ClientInfo ci, out _))
            {
                api.SendAPIError(401, "Could not authorize client!");
            }
            else
            {
                api.SendAPIOk(this, ("auth", Convert.ToBase64String(ci.SessionAuthToken)));
            }
        }

        public override void Construct(HTTPAPIEndpoint api, Dictionary<string, string> kvs)
        {
            if (!this.TryGet(kvs, "id", out this._id))
            {
                api.SendAPIError(400, "Missing ID field for authorization request!");
                return;
            }

            if (!this.TryGet(kvs, "secret", out string secretStr))
            {
                api.SendAPIError(400, "Missing secret field for authorization request!");
                return;
            }

            this._secret = Convert.FromBase64String(secretStr);
        }
    }
}
