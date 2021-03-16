using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Shipping;
using NopBrasil.Plugin.Shipping.Correios.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace NopBrasil.Plugin.Shipping.Correios.Service
{
    public class CorreiosService : ICorreiosService
    {
        //colocar as unidades de medida e moeda utilizadas como configuração
        private const string MEASURE_WEIGHT_SYSTEM_KEYWORD = "kg";

        private const string MEASURE_DIMENSION_SYSTEM_KEYWORD = "centimeter";
        private const string CURRENCY_CODE = "BRL";
        //colocar o tamanho/peso mínimo/máximo permitido dos produtos como configuração

        private readonly IMeasureService _measureService;
        private readonly IShippingService _shippingService;
        private readonly CorreiosSettings _correiosSettings;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IAddressService _addressService;

        public CorreiosService(IMeasureService measureService, IShippingService shippingService, CorreiosSettings correiosSettings,
            ICurrencyService currencyService, CurrencySettings currencySettings, IAddressService addressService)
        {
            _measureService = measureService;
            _shippingService = shippingService;
            _correiosSettings = correiosSettings;
            _currencyService = currencyService;
            _currencySettings = currencySettings;
            _addressService = addressService;
        }

        public async Task<WSCorreiosCalcPrecoPrazo.cResultado> RequestCorreios(GetShippingOptionRequest getShippingOptionRequest)
        {
            Binding binding = new BasicHttpBinding();
            binding.Name = "CalcPrecoPrazoWSSoap";

            getShippingOptionRequest.ZipPostalCodeFrom = await GetZipPostalCodeFrom(getShippingOptionRequest);

            decimal length, width, height;
            (width, length, height) = await GetDimensions(getShippingOptionRequest);
            var weight = GetWeight(getShippingOptionRequest);
            var declaredValue = await GetDeclaredValue(getShippingOptionRequest);

            EndpointAddress endpointAddress = new EndpointAddress(_correiosSettings.Url);
            var services = GetSelectecServices(_correiosSettings);
            WSCorreiosCalcPrecoPrazo.CalcPrecoPrazoWSSoap wsCorreios = new WSCorreiosCalcPrecoPrazo.CalcPrecoPrazoWSSoapClient(binding, endpointAddress);
            var wsCorreiosServices = new List<WSCorreiosCalcPrecoPrazo.cServico>();
            for (int i = 0; i < services.Length; i++)
            {
                var r = wsCorreios.CalcPrecoPrazo(_correiosSettings.CompanyCode ?? "", _correiosSettings.Password ?? "",
                        services[i], getShippingOptionRequest.ZipPostalCodeFrom,
                        getShippingOptionRequest.ShippingAddress.ZipPostalCode,
                        weight.ToString(),
                        1, length,
                        height, width,
                        0,
                        "N",
                        declaredValue, "N");
                wsCorreiosServices.AddRange(r.Servicos.ToList());
            }
            return new WSCorreiosCalcPrecoPrazo.cResultado { Servicos = wsCorreiosServices.ToArray() };
        }

        private async Task<string> GetZipPostalCodeFrom(GetShippingOptionRequest getShippingOptionRequest)
        {
            if ((getShippingOptionRequest.WarehouseFrom != null) && (!string.IsNullOrEmpty((await _addressService.GetAddressByIdAsync(getShippingOptionRequest.WarehouseFrom.AddressId))?.ZipPostalCode)))
                return (await _addressService.GetAddressByIdAsync(getShippingOptionRequest.WarehouseFrom.AddressId)).ZipPostalCode;
            if (!string.IsNullOrEmpty(getShippingOptionRequest.ZipPostalCodeFrom))
                return getShippingOptionRequest.ZipPostalCodeFrom;
            return _correiosSettings.PostalCodeFrom;
        }

        private async Task<decimal> GetDeclaredValue(GetShippingOptionRequest shippingOptionRequest)
        {
            decimal declaredValue = await GetConvertedRateFromPrimaryCurrency(shippingOptionRequest.Items.Sum(item => item.Product.Price));
            return Math.Max(declaredValue, _correiosSettings.DeclaredMinimumValue);
        }

        private async Task<int> GetWeight(GetShippingOptionRequest shippingOptionRequest)
        {
            var usedMeasureWeight = await _measureService.GetMeasureWeightBySystemKeywordAsync(MEASURE_WEIGHT_SYSTEM_KEYWORD);
            if (usedMeasureWeight == null)
                throw new NopException($"Correios shipping service. Could not load \"{MEASURE_WEIGHT_SYSTEM_KEYWORD}\" measure weight");

            int weight = Convert.ToInt32(Math.Ceiling(await _measureService.ConvertFromPrimaryMeasureWeightAsync(await _shippingService.GetTotalWeightAsync(shippingOptionRequest), usedMeasureWeight)));
            return AcceptedDimensions(weight, _correiosSettings.MinimumWeight, _correiosSettings.MaximumWeight);
        }

        private async Task<(decimal width, decimal length, decimal height)> GetDimensions(GetShippingOptionRequest shippingOptionRequest)
        {
            decimal length;
            decimal height;
            decimal width;

            var usedMeasureDimension = await _measureService.GetMeasureDimensionBySystemKeywordAsync(MEASURE_DIMENSION_SYSTEM_KEYWORD);
            if (usedMeasureDimension == null)
                throw new NopException($"Correios shipping service. Could not load \"{MEASURE_DIMENSION_SYSTEM_KEYWORD}\" measure dimension");

            (width, length, height) = await _shippingService.GetDimensionsAsync(shippingOptionRequest.Items);

            length = await _measureService.ConvertFromPrimaryMeasureDimensionAsync(length, usedMeasureDimension);
            length = AcceptedDimensions(length, _correiosSettings.MinimumLength, _correiosSettings.MaximumLength);

            height = await _measureService.ConvertFromPrimaryMeasureDimensionAsync(height, usedMeasureDimension);
            height = AcceptedDimensions(height, _correiosSettings.MinimumHeight, _correiosSettings.MaximumHeight);

            width = await _measureService.ConvertFromPrimaryMeasureDimensionAsync(width, usedMeasureDimension);
            width = AcceptedDimensions(width, _correiosSettings.MinimumWidth, _correiosSettings.MaximumWidth);
            return (width, length, height);
        }

        public async Task<decimal> GetConvertedRateFromPrimaryCurrency(decimal rate)
        {
            return GetConvertedRate(rate, await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId), await GetSupportedCurrency());
        }

        public async Task<decimal> GetConvertedRateToPrimaryCurrency(decimal rate)
        {
            return GetConvertedRate(rate, await GetSupportedCurrency(), await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId));
        }

        private decimal GetConvertedRate(decimal rate, Currency source, Currency target)
        {
            // TODO: Verificar como recuperar o valor para passar em "amount" no método _currencyService.ConvertCurrency(decimal amount, decimal exchangeRate)
            //// Código original
            //return (source.CurrencyCode == target.CurrencyCode) ? rate : _currencyService.ConvertCurrency(rate, source, target);
            return rate;
        }

        private async Task<Currency> GetSupportedCurrency()
        {
            var currency = await _currencyService.GetCurrencyByCodeAsync(CURRENCY_CODE);
            if (currency == null)
                throw new NopException($"Correios shipping service. Could not load \"{CURRENCY_CODE}\" currency");
            return currency;
        }

        private string[] GetSelectecServices(CorreiosSettings correioSettings)
        {
            List<string> s = new List<string>();
            correioSettings.ServicesOffered.RemoveLastIfEndsWith(":").Split(':').ToList().ForEach(service => s.Add(service?.Remove(0, 1).Replace("]", "")));
            return s.ToArray();
        }

        private decimal AcceptedDimensions(decimal value, decimal minimum, decimal maximum) => Math.Min(Math.Max(value, minimum), maximum);

        private int AcceptedDimensions(int value, int minimum, int maximum) => Math.Min(Math.Max(value, minimum), maximum);

        public async Task<bool> DenyProductShipping(List<Product> shippingItens)
        {
            var usedMeasureDimension = await _measureService.GetMeasureDimensionBySystemKeywordAsync(MEASURE_DIMENSION_SYSTEM_KEYWORD);
            if (usedMeasureDimension == null)
                throw new NopException($"Correios shipping service. Could not load \"{MEASURE_DIMENSION_SYSTEM_KEYWORD}\" measure dimension");

            foreach (var item in shippingItens)
            {
                var length = await _measureService.ConvertFromPrimaryMeasureDimensionAsync(item.Length, usedMeasureDimension);
                var height = await _measureService.ConvertFromPrimaryMeasureDimensionAsync(item.Height, usedMeasureDimension);
                var width = await _measureService.ConvertFromPrimaryMeasureDimensionAsync(item.Width, usedMeasureDimension);

                if (length > _correiosSettings.MaximumLength
                    || height > _correiosSettings.MaximumHeight
                    || width > _correiosSettings.MaximumWidth)
                {
                    return true;
                }
            }

            decimal total = shippingItens.Sum(item => item.Price);
            decimal declaredValue = await GetConvertedRateFromPrimaryCurrency(total);

            // TODO: Restringir categorias
            return declaredValue > 3000.0M;
        }

        WSCorreiosCalcPrecoPrazo.cResultado ICorreiosService.RequestCorreios(GetShippingOptionRequest getShippingOptionRequest)
        {
            throw new NotImplementedException();
        }

        decimal ICorreiosService.GetConvertedRateToPrimaryCurrency(decimal rate)
        {
            throw new NotImplementedException();
        }

        bool ICorreiosService.DenyProductShipping(List<Product> shippingItens)
        {
            throw new NotImplementedException();
        }
    }
}