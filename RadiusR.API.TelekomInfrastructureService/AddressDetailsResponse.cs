using RezaB.API.WebService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.TelekomInfrastructureService
{
    [DataContract]
    public class AddressDetailsResponse
    {
        [DataMember]
        public long AddressNo { get; set; }
        [DataMember]
        public long ProvinceID { get; set; }
        [DataMember]
        public string ProvinceName { get; set; }
        [DataMember]
        public long DistrictID { get; set; }
        [DataMember]
        public string DistrictName { get; set; }
        [DataMember]
        public long RuralCode { get; set; }
        [DataMember]
        public long NeighbourhoodID { get; set; }
        [DataMember]
        public string NeighbourhoodName { get; set; }
        [DataMember]
        public long StreetID { get; set; }
        [DataMember]
        public string StreetName { get; set; }
        [DataMember]
        public long DoorID { get; set; }
        [DataMember]
        public string DoorNo { get; set; }
        [DataMember]
        public long ApartmentID { get; set; }
        [DataMember]
        public string ApartmentNo { get; set; }
        [DataMember]
        public string AddressText { get; set; }
    }
    [DataContract]
    public partial class CustomerServiceAddressDetailsResponse : BaseResponse<AddressDetailsResponse, SHA1>
    {
        public CustomerServiceAddressDetailsResponse(string passwordHash, BaseRequest<SHA1> baseRequest) : base(passwordHash, baseRequest) { }
        [DataMember]
        public AddressDetailsResponse AddressDetailsResponse
        {
            get { return Data; }
            set { Data = value; }
        }

    }
}
