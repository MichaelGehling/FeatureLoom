using BenchmarkDotNet.Attributes;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.VSDiagnostics;

namespace FeatureLoom.PerformanceTests.JsonSerializer;

/// <summary>
/// Compares the reference handling variants against each other and against System.Text.Json and
/// Newtonsoft.Json, each with and without reference preservation.
///
/// The measured graph contains genuinely shared instances (an address referenced twice, a customer
/// back-reference and products referenced from order lines), so the reference preserving variants
/// actually have to emit refs instead of duplicating the objects.
///
/// Recorded baseline (.NET 10, output size measured separately):
///
///   Method                                Mean     Ratio    Bytes
///   Feature_NoRefCheck                    2.354 us  1.00      1618
///   Feature_OnLoopReplaceByRef            3.098 us  1.32      1618
///   Feature_AlwaysReplaceByRef_JsonPath   2.796 us  1.19       954
///   Feature_AlwaysReplaceByRef_IdBased    2.607 us  1.11      1046
///   SystemText_NoRefCheck                 4.836 us  2.06      1618
///   SystemText_Preserve                   3.837 us  1.63      1046
///   Newtonsoft_NoRefCheck                 9.104 us  3.87      1620
///   Newtonsoft_Preserve                   6.906 us  2.94       955
///
/// Two results are worth remembering:
///
/// 1. System.Text.Json and Newtonsoft get *faster* with preservation enabled, because their output
///    shrinks by roughly a third. Their cost per written byte actually gets worse. Our encoder is
///    fast enough that the saved bytes no longer pay for the tracking, which is why our preserving
///    modes are slightly slower than NoRefCheck rather than faster.
///
/// 2. OnLoopReplaceByRef produces byte identical output to NoRefCheck on this acyclic graph, so its
///    +32% is pure, unamortized bookkeeping overhead. A CPU profile showed this cost is spread
///    thinly across inlined item info handling rather than concentrated in a single hotspot, so
///    there is no obvious optimization target. See the doc comments on ReferenceCheck.
/// </summary>
[CsvMeasurementsExporter]
[HtmlExporter]
[MinIterationCount(25)]
[MaxIterationCount(100)]
[CPUUsageDiagnoser]
public class SerializeReferenceHandlingTest
{
    public class Address
    {
        public string Type;
        public string Street;
        public string City;
        public string Zip;
        public string Country;
    }

    public class Product
    {
        public string Sku;
        public string Name;
        public double Price;
    }

    public class OrderLine
    {
        public int LineNo;
        public Product Product;
        public int Qty;
        public double Discount;
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

    public class Order
    {
        public string OrderNo;
        public string Created;
        public Customer CustomerRef;
        public List<OrderLine> Lines;
        public Product PreferredProduct;
    }

    public class Catalog
    {
        public List<Product> Products;
    }

    public class Root
    {
        public string Tenant;
        public string RequestId;
        public Customer Customer;
        public Catalog Catalog;
        public Order Order;
    }

    static readonly Serialization.JsonSerializer featureNoRefCheck = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings { typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo, referenceCheck = Serialization.JsonSerializer.ReferenceCheck.NoRefCheck, });
    static readonly Serialization.JsonSerializer featureOnLoop = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings { typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo, referenceCheck = Serialization.JsonSerializer.ReferenceCheck.OnLoopReplaceByRef, });
    static readonly Serialization.JsonSerializer featureAlwaysJsonPath = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings { typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo, referenceCheck = Serialization.JsonSerializer.ReferenceCheck.AlwaysReplaceByRef, referenceFormat = Serialization.JsonSerializer.ReferenceFormat.JsonPath, });
    static readonly Serialization.JsonSerializer featureAlwaysIdBased = new Serialization.JsonSerializer(new Serialization.JsonSerializer.Settings { typeInfoHandling = Serialization.JsonSerializer.TypeInfoHandling.AddNoTypeInfo, referenceCheck = Serialization.JsonSerializer.ReferenceCheck.AlwaysReplaceByRef, referenceFormat = Serialization.JsonSerializer.ReferenceFormat.IdBased, });
    static readonly JsonSerializerOptions stjPlain = new JsonSerializerOptions
    {
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    static readonly JsonSerializerOptions stjPreserve = new JsonSerializerOptions
    {
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReferenceHandler = ReferenceHandler.Preserve,
    };
    static readonly JsonSerializerSettings newtonsoftPlain = new JsonSerializerSettings();
    static readonly JsonSerializerSettings newtonsoftPreserve = new JsonSerializerSettings
    {
        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
    };
    MemoryStream memoryStream = new MemoryStream(1024 * 1024);
    Root graph;
    [GlobalSetup]
    public void Setup()
    {
        var billing = new Address
        {
            Type = "Billing",
            Street = "Main Street 12",
            City = "Hamburg",
            Zip = "20095",
            Country = "DE"
        };
        var shipping = new Address
        {
            Type = "Shipping",
            Street = "Logistics Park 7",
            City = "Hamburg",
            Zip = "20539",
            Country = "DE"
        };
        var customer = new Customer
        {
            Id = 120045,
            Name = "Contoso Retail GmbH",
            Addresses = new List<Address>
            {
                billing,
                shipping
            },
            // shared instances -> these become refs
            PrimaryAddress = shipping,
            ShippingAddress = shipping,
            Tags = new List<string>
            {
                "vip",
                "b2b",
                "priority"
            },
        };
        var p0 = new Product
        {
            Sku = "A-100",
            Name = "Industrial Widget",
            Price = 129.99
        };
        var p1 = new Product
        {
            Sku = "B-200",
            Name = "Service Plan",
            Price = 19.99
        };
        var p2 = new Product
        {
            Sku = "C-300",
            Name = "Replacement Kit",
            Price = 49.50
        };
        graph = new Root
        {
            Tenant = "north-eu",
            RequestId = "REQ-2026-05-14-7781",
            Customer = customer,
            Catalog = new Catalog
            {
                Products = new List<Product>
                {
                    p0,
                    p1,
                    p2
                }
            },
            Order = new Order
            {
                OrderNo = "SO-874221",
                Created = "2026-05-14T10:21:00Z",
                // back reference to an already written object
                CustomerRef = customer,
                Lines = new List<OrderLine>
                {
                    new OrderLine
                    {
                        LineNo = 1,
                        Product = p0,
                        Qty = 4,
                        Discount = 0.05
                    },
                    new OrderLine
                    {
                        LineNo = 2,
                        Product = p2,
                        Qty = 1,
                        Discount = 0.00
                    },
                },
                PreferredProduct = p0,
            },
        };
    }

    [Benchmark(Baseline = true)]
    public void Feature_NoRefCheck()
    {
        memoryStream.Position = 0;
        featureNoRefCheck.Serialize(memoryStream, graph);
    }

    [Benchmark]
    public void Feature_OnLoopReplaceByRef()
    {
        memoryStream.Position = 0;
        featureOnLoop.Serialize(memoryStream, graph);
    }

    [Benchmark]
    public void Feature_AlwaysReplaceByRef_JsonPath()
    {
        memoryStream.Position = 0;
        featureAlwaysJsonPath.Serialize(memoryStream, graph);
    }

    [Benchmark]
    public void Feature_AlwaysReplaceByRef_IdBased()
    {
        memoryStream.Position = 0;
        featureAlwaysIdBased.Serialize(memoryStream, graph);
    }

    [Benchmark]
    public void SystemText_NoRefCheck()
    {
        memoryStream.Position = 0;
        System.Text.Json.JsonSerializer.Serialize(memoryStream, graph, stjPlain);
    }

    [Benchmark]
    public void SystemText_Preserve()
    {
        memoryStream.Position = 0;
        System.Text.Json.JsonSerializer.Serialize(memoryStream, graph, stjPreserve);
    }

    [Benchmark]
    public void Newtonsoft_NoRefCheck()
    {
        memoryStream.Position = 0;
        var json = JsonConvert.SerializeObject(graph, newtonsoftPlain);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        memoryStream.Write(bytes, 0, bytes.Length);
    }

    [Benchmark]
    public void Newtonsoft_Preserve()
    {
        memoryStream.Position = 0;
        var json = JsonConvert.SerializeObject(graph, newtonsoftPreserve);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        memoryStream.Write(bytes, 0, bytes.Length);
    }
}