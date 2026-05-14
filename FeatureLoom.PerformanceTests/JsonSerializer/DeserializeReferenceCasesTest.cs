using BenchmarkDotNet.Attributes;
using FeatureLoom.PerformanceTests.AsyncManualResetEventPerformance;
using FeatureLoom.Serialization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

[MemoryDiagnoser]
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(250)]
[MaxIterationCount(1000)]
public class DeserializeReferenceCasesTest
{
    private static readonly JsonDeserializer featureJsonDeserializer = new JsonDeserializer(settings =>
    {
        settings.initialBufferSize = 1024 * 1024 * 10;
        settings.dataAccess = JsonDeserializer.DataAccess.PublicAndPrivateFields;
        settings.proposedTypeMode = JsonDeserializer.Settings.ProposedTypeMode.Ignore;
        settings.referenceResolutionMode = JsonDeserializer.Settings.ReferenceResolutionMode.EnabledByDefault;
        settings.populateExistingMembers = true;
        settings.useStringCache = true;
    });

    private static readonly JsonSerializerSettings newtonsoftSettings = new JsonSerializerSettings
    {
        PreserveReferencesHandling = PreserveReferencesHandling.All,
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize
    };

    private static readonly JsonSerializerOptions stjOptions = new JsonSerializerOptions
    {
        IncludeFields = true,
        ReferenceHandler = ReferenceHandler.Preserve
    };

    private MemoryStream idVariantStream = new(1024 * 1024);
    private MemoryStream jsonPathVariantStream = new(1024 * 1024);

    [Params(-1000, -100, -10, -1)]
    public int iterations;

    [GlobalSetup]
    public void GlobalPrepare()
    {
        // Realistic shape: customer + addresses + line items + small number of refs
        // refs: PrimaryAddress, ShippingAddress (same), PreferredProduct, LastOrder.CustomerRef

        string jsonId =
            "{"
            + "\"$id\":\"root\","
            + "\"Tenant\":\"north-eu\","
            + "\"RequestId\":\"REQ-2026-05-14-7781\","
            + "\"Customer\":{"
                + "\"$id\":\"cust1\","
                + "\"Id\":120045,"
                + "\"Name\":\"Contoso Retail GmbH\","
                + "\"Addresses\":{\"$id\":\"addrList\",\"$values\":["
                    + "{\"$id\":\"addr0\",\"Type\":\"Billing\",\"Street\":\"Main Street 12\",\"City\":\"Hamburg\",\"Zip\":\"20095\",\"Country\":\"DE\"},"
                    + "{\"$id\":\"addr1\",\"Type\":\"Shipping\",\"Street\":\"Logistics Park 7\",\"City\":\"Hamburg\",\"Zip\":\"20539\",\"Country\":\"DE\"}"
                + "]},"
                + "\"PrimaryAddress\":{\"$ref\":\"addr1\"},"
                + "\"ShippingAddress\":{\"$ref\":\"addr1\"},"
                + "\"Tags\":{\"$id\":\"tags1\",\"$values\":[\"vip\",\"b2b\",\"priority\"]}"
            + "},"
            + "\"Catalog\":{\"$id\":\"catalog\","
                + "\"Products\":{\"$id\":\"products\",\"$values\":["
                    + "{\"$id\":\"prod0\",\"Sku\":\"A-100\",\"Name\":\"Industrial Widget\",\"Price\":129.99},"
                    + "{\"$id\":\"prod1\",\"Sku\":\"B-200\",\"Name\":\"Service Plan\",\"Price\":19.99},"
                    + "{\"$id\":\"prod2\",\"Sku\":\"C-300\",\"Name\":\"Replacement Kit\",\"Price\":49.50}"
                + "]}"
            + "},"
            + "\"Order\":{"
                + "\"$id\":\"order1\","
                + "\"OrderNo\":\"SO-874221\","
                + "\"Created\":\"2026-05-14T10:21:00Z\","
                + "\"CustomerRef\":{\"$ref\":\"cust1\"},"
                + "\"Lines\":{\"$id\":\"lines1\",\"$values\":["
    + "{\"LineNo\":1,\"Product\":{\"$ref\":\"prod0\"},\"Qty\":4,\"Discount\":0.05},"
    + "{\"LineNo\":2,\"Product\":{\"$ref\":\"prod2\"},\"Qty\":1,\"Discount\":0.00}"
+ "]},"
                + "\"PreferredProduct\":{\"$ref\":\"prod0\"}"
            + "}"
            + "}";

        string jsonPath =
            "{"
            + "\"Tenant\":\"north-eu\","
            + "\"RequestId\":\"REQ-2026-05-14-7781\","
            + "\"Customer\":{"
                + "\"Id\":120045,"
                + "\"Name\":\"Contoso Retail GmbH\","
                + "\"Addresses\":["
                + "{\"Type\":\"Billing\",\"Street\":\"Main Street 12\",\"City\":\"Hamburg\",\"Zip\":\"20095\",\"Country\":\"DE\"},"
                + "{\"Type\":\"Shipping\",\"Street\":\"Logistics Park 7\",\"City\":\"Hamburg\",\"Zip\":\"20539\",\"Country\":\"DE\"}"
            + "],"
            + "\"PrimaryAddress\":{\"$ref\":\"$.Customer.Addresses[1]\"},"
            + "\"ShippingAddress\":{\"$ref\":\"$.Customer.Addresses[1]\"},"
            + "\"Tags\":[\"vip\",\"b2b\",\"priority\"]"
        + "},"
        + "\"Catalog\":{"
            + "\"Products\":["
                + "{\"Sku\":\"A-100\",\"Name\":\"Industrial Widget\",\"Price\":129.99},"
                + "{\"Sku\":\"B-200\",\"Name\":\"Service Plan\",\"Price\":19.99},"
                + "{\"Sku\":\"C-300\",\"Name\":\"Replacement Kit\",\"Price\":49.50}"
            + "]"
        + "},"
        + "\"Order\":{"
            + "\"OrderNo\":\"SO-874221\","
            + "\"Created\":\"2026-05-14T10:21:00Z\","
            + "\"CustomerRef\":{\"$ref\":\"$.Customer\"},"
            + "\"Lines\":["
                + "{\"LineNo\":1,\"Product\":{\"$ref\":\"$.Catalog.Products[0]\"},\"Qty\":4,\"Discount\":0.05},"
                + "{\"LineNo\":2,\"Product\":{\"$ref\":\"$.Catalog.Products[2]\"},\"Qty\":1,\"Discount\":0.00}"
            + "],"
            + "\"PreferredProduct\":{\"$ref\":\"$.Catalog.Products[0]\"}"
        + "}"
        + "}";

        idVariantStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonId));
        jsonPathVariantStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonPath));
    }

    [IterationSetup]
    public void Prepare()
    {
        idVariantStream.Position = 0;
        jsonPathVariantStream.Position = 0;
        iterations = Math.Abs(iterations);
    }

    // FeatureLoom: JsonPath variant
    [Benchmark]
    public void DeserializeReferenceCases_Feature_JsonPathVariant()
    {
        for (int i = 0; i < iterations; i++)
        {
            jsonPathVariantStream.Position = 0;
            featureJsonDeserializer.TryDeserialize(jsonPathVariantStream, out ReferenceCaseRoot _);
        }
    }

    // FeatureLoom: $id variant
    [Benchmark(Baseline = true)]
    public void DeserializeReferenceCases_Feature_IdVariant()
    {
        for (int i = 0; i < iterations; i++)
        {
            idVariantStream.Position = 0;
            featureJsonDeserializer.TryDeserialize(idVariantStream, out ReferenceCaseRoot _);
        }
    }    

    // Newtonsoft: $id variant
    [Benchmark]
    public void DeserializeReferenceCases_Newtonsoft_IdVariant()
    {
        for (int i = 0; i < iterations; i++)
        {
            idVariantStream.Position = 0;
            using var reader = new StreamReader(idVariantStream, Encoding.UTF8, false, 1024, true);
            string text = reader.ReadToEnd();
            _ = JsonConvert.DeserializeObject<ReferenceCaseRoot>(text, newtonsoftSettings);
        }
    }

    // System.Text.Json: $id variant
    [Benchmark]
    public void DeserializeReferenceCases_SystemText_IdVariant()
    {
        for (int i = 0; i < iterations; i++)
        {
            idVariantStream.Position = 0;
            _ = System.Text.Json.JsonSerializer.Deserialize<ReferenceCaseRoot>(idVariantStream, stjOptions);
        }
    }

    public class ReferenceCaseRoot
    {
        public string Tenant;
        public string RequestId;
        public Customer Customer;
        public Catalog Catalog;
        public Order Order;
    }

    public class Customer
    {
        public int Id;
        public string Name;
        public List<Address> Addresses;
        public Address PrimaryAddress;
        public Address ShippingAddress;
        public List<string> Tags;
    }

    public class Address
    {
        public string Type;
        public string Street;
        public string City;
        public string Zip;
        public string Country;
    }

    public class Catalog
    {
        public List<Product> Products;
    }

    public class Product
    {
        public string Sku;
        public string Name;
        public double Price;
    }

    public class Order
    {
        public string OrderNo;
        public string Created;
        public Customer CustomerRef;
        public List<OrderLine> Lines;
        public Product PreferredProduct;
    }

    public class OrderLine
    {
        public int LineNo;
        public Product Product;
        public int Qty;
        public double Discount;
    }
}