using RadiusR.Localization.Model;
using RezaB.API.WebService;
using RezaB.Data.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Web;

namespace RadiusR.API.CustomerWebService.Responses.PartnerResponses
{
    [DataContract]
    public class AuthenticationResponse
    {
        [DataMember]
        public string PhoneNo { get; set; }
        [DataMember]
        public bool IsAuthenticated { get; set; }

        [DataMember]
        public int UserID { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string SetupServiceUser { get; set; }

        [DataMember]
        public string SetupServiceHash { get; set; }

        [DataMember]
        public PermissionResult[] Permissions { get; set; }
        [DataMember]
        public WorkAreaResult[] WorkAreas { get; set; }
        [DataContract]
        public class WorkAreaResult
        {
            [DataMember]
            public long WorkAreaId { get; set; }
            [DataMember]
            public long ProvinceID { get; set; }
            [DataMember]
            public long? DistrictID { get; set; }
            [DataMember]
            public long? RuralCode { get; set; }
            [DataMember]
            public long? NeighbourhoodID { get; set; }
            [DataMember]
            public string ProvinceName { get; set; }
            [DataMember]
            public string DistrictName { get; set; }
            [DataMember]
            public string NeighbourhoodName { get; set; }

        }
        [DataContract]
        public class PermissionResult
        {
            [DataMember]
            public short ID { get; set; }

            [DataMember]
            public string Name { get; set; }

            private PermissionResult() { }

            public PermissionResult(short id)
            {
                ID = id;

                try
                {
                    var resourceKey = Enum.GetName(typeof(RadiusR.DB.Enums.PartnerPermissions), id);
                    Name = resourceKey;
                }
                catch
                {
                    Name = "N/A";
                }
            }

            private CultureInfo CreateCulture(string cultureName)
            {
                var currentCulture = CultureInfo.InvariantCulture;
                try
                {
                    currentCulture = CultureInfo.CreateSpecificCulture(cultureName);
                }
                catch { }
                return currentCulture;
            }
        }
    }
    [DataContract]
    public partial class PartnerServiceAuthenticationResponse : BaseResponse<AuthenticationResponse, SHA256>
    {
        public PartnerServiceAuthenticationResponse(string passwordHash, BaseRequest<SHA256> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AuthenticationResponse AuthenticationResponse { get { return Data; } set { Data = value; } }
    }
}