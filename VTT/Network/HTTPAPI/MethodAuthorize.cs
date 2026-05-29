namespace VTT.Network.HTTPAPI
{
    using System;
    using System.Collections.Generic;

    public class MethodAuthorize : APIMethod
    {
        public override string Identifier => "authorize";

        private Guid _id;
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
