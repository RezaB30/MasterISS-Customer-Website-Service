using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace RadiusR.API.TelekomInfrastructureService
{
    [ServiceContract]
    public interface ITelekomInfrastructureService
    {
        [OperationContract]
        CustomerServiceNameValuePair GetProvinces(CustomerServiceProvincesRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetProvinceDistricts(CustomerServiceNameValuePairRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetDistrictRuralRegions(CustomerServiceNameValuePairRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetRuralRegionNeighbourhoods(CustomerServiceNameValuePairRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetNeighbourhoodStreets(CustomerServiceNameValuePairRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetStreetBuildings(CustomerServiceNameValuePairRequest request);
        [OperationContract]
        CustomerServiceNameValuePair GetBuildingApartments(CustomerServiceNameValuePairRequest request);
        [OperationContract]
        CustomerServiceAddressDetailsResponse GetApartmentAddress(CustomerServiceAddressDetailsRequest request);
        [OperationContract]
        CustomerServiceServiceAvailabilityResponse ServiceAvailability(CustomerServiceServiceAvailabilityRequest request);
    }
}
