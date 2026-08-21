using System;
using System.Collections.Generic;
using FeatureLoom.Collections;
using Xunit;

namespace FeatureLoom.Serialization
{
    public class JsonSerializerCustomTypeWriterTests
    {
        [Fact]
        public void CustomValueWriter_ReplacesBuiltInWriter()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareValueWriter<Money>((value, item) => value.WriteString($"{item.Amount} {item.Currency}"))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"12 EUR\"", serializer.Serialize(new Money { Amount = 12, Currency = "EUR" }));
        }

        [Fact]
        public void CustomObjectWriter_WritesDeclaredFields()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Person>(obj => obj
                .AddField("name", p => p.Name)
                .AddRawField("age", (raw, p) => raw.WriteInt(p.Age)))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"name\":\"Ann\",\"age\":42}", serializer.Serialize(new Person { Name = "Ann", Age = 42 }));
        }

        [Fact]
        public void CustomObjectWriter_WithoutFields_WritesEmptyObject()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Person>(_ => { })));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{}", serializer.Serialize(new Person { Name = "Ann", Age = 42 }));
        }

        [Fact]
        public void CustomArrayWriter_WritesElementsWithItemWriter()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Tags>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareArrayWriter<Tags, string>(t => t.Values)));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("[\"a\",\"b\"]", serializer.Serialize(new Tags { Values = new List<string> { "a", "b" } }));
        }

        [Fact]
        public void CustomRawWriter_ControlsAllTokens()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareRawWriter<Money>((raw, item) =>
            {
                raw.OpenArray();
                raw.WriteInt(item.Amount);
                raw.WriteComma();
                raw.WriteString(item.Currency);
                raw.CloseArray();
            })));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("[12,\"EUR\"]", serializer.Serialize(new Money { Amount = 12, Currency = "EUR" }));
        }

        [Fact]
        public void CustomTypeWriter_AppliesToAssignableTypes_WhenRequested()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteString($"base:{item.Code}")),
                true));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"base:7\"", serializer.Serialize(new DerivedItem { Code = 7 }));
        }

        [Fact]
        public void CustomTypeWriter_PredicateAddsTypes_ButNeverExcludesT()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteString($"matched:{item.Code}")),
                type => type.Name.StartsWith("Derived")));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"matched:7\"", serializer.Serialize(new DerivedItem { Code = 7 }));
            // T is always covered, even though the predicate does not accept it.
            Assert.Equal("\"matched:7\"", serializer.Serialize(new BaseItem { Code = 7 }));
        }

        [Fact]
        public void CustomTypeWriter_PredicateDoesNotMatchUnrelatedTypes()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteString($"matched:{item.Code}")),
                type => type.Name.StartsWith("Derived")));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"Name\":\"Ann\",\"Age\":42}", serializer.Serialize(new Person { Name = "Ann", Age = 42 }));
        }

        [Fact]
        public void CustomObjectWriter_NestedValuesUseTheirOwnCustomWriter()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareValueWriter<Money>((value, item) => value.WriteString($"{item.Amount} {item.Currency}"))));
            settings.ConfigureType<Order>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Order>(obj => obj
                .AddField("total", o => o.Total))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"total\":\"5 USD\"}",
                serializer.Serialize(new Order { Total = new Money { Amount = 5, Currency = "USD" } }));
        }

        [Fact]
        public void CustomTypeWriter_ExactTypeMatchWins_OverEarlierAssignableMatch()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteString($"base:{item.Code}")),
                type => typeof(BaseItem).IsAssignableFrom(type)));
            settings.ConfigureType<DerivedItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<DerivedItem>((value, item) => value.WriteString($"derived:{item.Code}"))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"derived:7\"", serializer.Serialize(new DerivedItem { Code = 7 }));
            Assert.Equal("\"base:3\"", serializer.Serialize(new BaseItem { Code = 3 }));
        }

        [Fact]
        public void CustomTypeWriter_ExactTypeMatchWins_OverEarlierPredicateMatch()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteString($"predicate:{item.Code}")),
                type => typeof(BaseItem).IsAssignableFrom(type)));
            settings.ConfigureType<DerivedItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<DerivedItem>((value, item) => value.WriteString($"derived:{item.Code}"))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"derived:7\"", serializer.Serialize(new DerivedItem { Code = 7 }));
        }

        [Fact]
        public void CustomTypeWriter_Precedence_RegistrationOrderMatters_AmongEquallySpecificMatches()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteString($"first:{item.Code}")),
                type => typeof(BaseItem).IsAssignableFrom(type)));
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteString($"second:{item.Code}")),
                type => type.Name.StartsWith("Derived")));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"first:7\"", serializer.Serialize(new DerivedItem { Code = 7 }));
        }

        [Fact]
        public void CustomObjectWriter_AddObject_WritesNestedObjectInline()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Order>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Order>(obj => obj
                .AddObject("total", o => o.Total, m => m
                    .AddField("amount", t => t.Amount)
                    .AddField("currency", t => t.Currency)))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"total\":{\"amount\":12,\"currency\":\"EUR\"}}",
                serializer.Serialize(new Order { Total = new Money { Amount = 12, Currency = "EUR" } }));
        }

        [Fact]
        public void CustomObjectWriter_AddObject_WritesNullLiteralForNullValue()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Order>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Order>(obj => obj
                .AddObject("total", o => o.Total, m => m.AddField("amount", t => t.Amount)))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"total\":null}", serializer.Serialize(new Order()));
        }

        [Fact]
        public void CustomObjectWriter_AddArray_WritesItemsWithTypeWriter()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Tags>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Tags>(obj => obj
                .AddArray("values", t => t.Values))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"values\":[\"a\",\"b\"]}", serializer.Serialize(new Tags { Values = new List<string> { "a", "b" } }));
            Assert.Equal("{\"values\":null}", serializer.Serialize(new Tags()));
        }

        [Fact]
        public void CustomObjectWriter_AddArray_WithNestedBuilder_WritesObjects()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Basket>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Basket>(obj => obj
                .AddArray("items", b => b.Items, m => m
                    .AddField("amount", t => t.Amount)))));

            var serializer = new JsonSerializer(settings);

            var basket = new Basket { Items = new List<Money> { new Money { Amount = 1 }, null } };
            Assert.Equal("{\"items\":[{\"amount\":1},null]}", serializer.Serialize(basket));
        }

        [Fact]
        public void CustomArrayWriter_WithNestedObjectBuilder_WritesObjects()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Basket>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareArrayWriter<Basket, Money>(
                b => b.Items,
                m => m.AddField("amount", t => t.Amount))));

            var serializer = new JsonSerializer(settings);

            var basket = new Basket { Items = new List<Money> { new Money { Amount = 1 }, null } };
            Assert.Equal("[{\"amount\":1},null]", serializer.Serialize(basket));
        }

        [Fact]
        public void CustomArrayWriter_WithRawItemWriter_WritesItems()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Tags>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareArrayWriter<Tags, string>(
                t => t.Values,
                (raw, value) => raw.WriteString(value.ToUpperInvariant()))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("[\"A\",\"B\"]", serializer.Serialize(new Tags { Values = new List<string> { "a", "b" } }));
        }

        [Fact]
        public void PrepareTypeWriter_WithDeviatingSettings_DoesNotAffectSharedWriter()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Order>(ts => ts.SetCustomTypeWriter(prep =>
            {
                var writeTotal = prep.PrepareTypeWriter<Money>(ms => ms.SetCustomTypeWriter(
                    p => p.PrepareValueWriter<Money>((value, m) => value.WriteInt(m.Amount))));
                return prep.PrepareRawWriter<Order>((raw, o) =>
                {
                    raw.OpenObject();
                    raw.WriteFieldName("total");
                    writeTotal(o.Total);
                    raw.CloseObject();
                });
            }));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"total\":12}", serializer.Serialize(new Order { Total = new Money { Amount = 12, Currency = "EUR" } }));
            // The deviating settings are local to the preparation, so Money written on its own
            // still uses the regular writer.
            Assert.Equal("{\"Amount\":12,\"Currency\":\"EUR\"}", serializer.Serialize(new Money { Amount = 12, Currency = "EUR" }));
        }

        [Fact]
        public void OpenGenericCustomWriter_IsUsedForEveryConstructedType()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>)));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"v\":42}", serializer.Serialize(new Wrapper<int> { Value = 42 }));
            Assert.Equal("{\"v\":\"x\"}", serializer.Serialize(new Wrapper<string> { Value = "x" }));
        }

        [Fact]
        public void OpenGenericCustomWriter_UsesTypeWriterOfGenericArgument()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>)));
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareValueWriter<Money>((value, m) => value.WriteString($"{m.Amount} {m.Currency}"))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"v\":\"12 EUR\"}", serializer.Serialize(new Wrapper<Money> { Value = new Money { Amount = 12, Currency = "EUR" } }));
        }

        [Fact]
        public void ClosedTypeCustomWriter_BeatsOpenGenericCustomWriter()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>)));
            settings.ConfigureType<Wrapper<int>>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareValueWriter<Wrapper<int>>((value, w) => value.WriteInt(w.Value))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("42", serializer.Serialize(new Wrapper<int> { Value = 42 }));
            Assert.Equal("{\"v\":\"x\"}", serializer.Serialize(new Wrapper<string> { Value = "x" }));
        }

        [Fact]
        public void OpenGenericCustomWriter_WithMismatchingArity_Throws()
        {
            var settings = new JsonSerializer.Settings();

            Assert.Throws<System.ArgumentException>(() =>
                settings.ConfigureGenericType(typeof(Pair<,>), ts => ts.SetCustomTypeWriter(typeof(WrapperWriter<>))));
        }

        [Fact]
        public void OpenGenericCustomWriter_WritingAnotherType_Throws()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureGenericType(typeof(Wrapper<>), ts => ts.SetCustomTypeWriter(typeof(ForeignWriter<>)));

            var serializer = new JsonSerializer(settings);

            Assert.Throws<System.ArgumentException>(() => serializer.Serialize(new Wrapper<int> { Value = 42 }));
        }

        [Fact]
        public void OpenGenericCustomWriter_WithMultipleTypeParams_IsUsed()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureGenericType(typeof(Pair<,>), ts => ts.SetCustomTypeWriter(typeof(PairWriter<,>)));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"f\":1,\"s\":\"a\"}", serializer.Serialize(new Pair<int, string> { First = 1, Second = "a" }));
            Assert.Equal("{\"f\":\"a\",\"s\":true}", serializer.Serialize(new Pair<string, bool> { First = "a", Second = true }));
        }

        [Fact]
        public void OpenGenericCustomWriter_WithSwappedTypeParams_Throws()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureGenericType(typeof(Pair<,>), ts => ts.SetCustomTypeWriter(typeof(SwappedPairWriter<,>)));

            var serializer = new JsonSerializer(settings);

            // The generic arguments are passed on positionally, so a definition declaring them in
            // another order does not write the type it was registered for.
            Assert.Throws<System.ArgumentException>(() => serializer.Serialize(new Pair<int, string> { First = 1, Second = "a" }));
        }

        [Fact]
        public void OpenGenericCustomWriter_WithThreeTypeParams_IsUsed()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureGenericType(typeof(Triple<,,>), ts => ts.SetCustomTypeWriter(typeof(TripleWriter<,,>)));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"a\":1,\"b\":\"x\",\"c\":true}", serializer.Serialize(new Triple<int, string, bool> { A = 1, B = "x", C = true }));
        }

        [Fact]
        public void CustomWriterDefinitionInstance_IsUsedForClosedType()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Wrapper<int>>(ts => ts.SetCustomTypeWriter(new WrapperWriter<int>()));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"v\":42}", serializer.Serialize(new Wrapper<int> { Value = 42 }));
        }

        [Fact]
        public void CustomWriterDefinitionInstance_CanCarryState()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(new PersonWriter("who")));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"who\":\"Ann\"}", serializer.Serialize(new Person { Name = "Ann", Age = 42 }));
        }

        private class Money
        {
            public int Amount;
            public string Currency;
        }

        private class Wrapper<T>
        {
            public T Value;
        }

        private class Pair<T1, T2>
        {
            public T1 First;
            public T2 Second;
        }

        private class WrapperWriter<T> : JsonSerializer.CustomTypeWriterDefinition<Wrapper<T>>
        {
            protected override JsonSerializer.CustomWriter<Wrapper<T>> Prepare(JsonSerializer.WriterPreparationApi api) =>
                api.PrepareObjectWriter<Wrapper<T>>(obj => obj.AddField("v", w => w.Value));
        }

        private class Triple<T1, T2, T3>
        {
            public T1 A;
            public T2 B;
            public T3 C;
        }

        private class TripleWriter<T1, T2, T3> : JsonSerializer.CustomTypeWriterDefinition<Triple<T1, T2, T3>>
        {
            protected override JsonSerializer.CustomWriter<Triple<T1, T2, T3>> Prepare(JsonSerializer.WriterPreparationApi api) =>
                api.PrepareObjectWriter<Triple<T1, T2, T3>>(obj => obj
                    .AddField("a", t => t.A)
                    .AddField("b", t => t.B)
                    .AddField("c", t => t.C));
        }

        private class PairWriter<T1, T2> : JsonSerializer.CustomTypeWriterDefinition<Pair<T1, T2>>
        {
            protected override JsonSerializer.CustomWriter<Pair<T1, T2>> Prepare(JsonSerializer.WriterPreparationApi api) =>
                api.PrepareObjectWriter<Pair<T1, T2>>(obj => obj
                    .AddField("f", p => p.First)
                    .AddField("s", p => p.Second));
        }

        /// <summary>
        /// Deliberately broken: it declares the generic arguments in the opposite order.
        /// </summary>
        private class SwappedPairWriter<T1, T2> : JsonSerializer.CustomTypeWriterDefinition<Pair<T2, T1>>
        {
            protected override JsonSerializer.CustomWriter<Pair<T2, T1>> Prepare(JsonSerializer.WriterPreparationApi api) =>
                api.PrepareObjectWriter<Pair<T2, T1>>(obj => obj.AddField("f", p => p.First));
        }

        private class PersonWriter : JsonSerializer.CustomTypeWriterDefinition<Person>
        {
            readonly string nameField;

            public PersonWriter(string nameField) => this.nameField = nameField;

            protected override JsonSerializer.CustomWriter<Person> Prepare(JsonSerializer.WriterPreparationApi api) =>
                api.PrepareObjectWriter<Person>(obj => obj.AddField(nameField, p => p.Name));
        }

        /// <summary>
        /// Deliberately broken: it does not write the type it is registered for.
        /// </summary>
        private class ForeignWriter<T> : JsonSerializer.CustomTypeWriterDefinition<Pair<T, T>>
        {
            protected override JsonSerializer.CustomWriter<Pair<T, T>> Prepare(JsonSerializer.WriterPreparationApi api) =>
                api.PrepareObjectWriter<Pair<T, T>>(obj => obj.AddField("f", p => p.First));
        }

        private class Person
        {
            public string Name;
            public int Age;
        }

        private class Tags
        {
            public List<string> Values;
        }

        private class Order
        {
            public Money Total;
        }

        private class Basket
        {
            public List<Money> Items;
        }

        private class BaseItem
        {
            public int Code;
        }

        private class DerivedItem : BaseItem
        {
        }

        [Fact]
        public void CustomWriter_WritesStringFromTextSegmentSlice()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareValueWriter<Money>((value, item) =>
                value.WriteString(new TextSegment("prefix-EUR-suffix", 7, 3)))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"EUR\"", serializer.Serialize(new Money { Amount = 12, Currency = "EUR" }));
        }

        [Fact]
        public void CustomWriter_WritesStringFromCharSpanSlice()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareValueWriter<Money>((value, item) =>
                value.WriteString("prefix-EUR-suffix".AsSpan(7, 3)))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"EUR\"", serializer.Serialize(new Money { Amount = 12, Currency = "EUR" }));
        }

        [Fact]
        public void CustomWriter_EscapesStringFromSpan()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareValueWriter<Money>((value, item) =>
                value.WriteString("a\"b\\c".AsSpan()))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("\"a\\\"b\\\\c\"", serializer.Serialize(new Money()));
        }

        [Fact]
        public void CustomWriter_WritesRawJsonFromPreparedFragment()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
            {
                var fragment = prep.PrepareRawJson("{\"kind\":\"money\"}");
                return prep.PrepareRawWriter<Money>((raw, item) => raw.WriteRawJson(fragment));
            }));

            var serializer = new JsonSerializer(settings);

            // Serialized twice to prove the prepared fragment stays usable across calls.
            Assert.Equal("{\"kind\":\"money\"}", serializer.Serialize(new Money()));
            Assert.Equal("{\"kind\":\"money\"}", serializer.Serialize(new Money()));
        }

        [Fact]
        public void CustomWriter_WritesRawJsonFromSpanAndTextSegment()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareRawWriter<Money>((raw, item) =>
            {
                raw.OpenArray();
                raw.WriteRawJson("[1,2,3]".AsSpan(1, 5));
                raw.WriteComma();
                raw.WriteRawJson(new TextSegment("xx42xx", 2, 2));
                raw.CloseArray();
            })));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("[1,2,3,42]", serializer.Serialize(new Money()));
        }

        [Fact]
        public void CustomWriter_WritesPreparedFieldNameFromPrepareApi()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
            {
                byte[] amountName = prep.PrepareFieldName("amount");
                return prep.PrepareRawWriter<Money>((raw, item) =>
                {
                    raw.OpenObject();
                    raw.WritePrepared(amountName);
                    raw.WriteInt(item.Amount);
                    raw.CloseObject();
                });
            }));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"amount\":12}", serializer.Serialize(new Money { Amount = 12 }));
        }

        [Fact]
        public void CustomWriter_WritesPreparedBytesFromSegmentAndSpan()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
            {
                byte[] amountName = prep.PrepareFieldName("amount");
                var asSegment = new ByteSegment(amountName);
                return prep.PrepareRawWriter<Money>((raw, item) =>
                {
                    raw.OpenObject();
                    raw.WritePrepared(asSegment);
                    raw.WriteInt(item.Amount);
                    raw.WriteComma();
                    raw.WritePrepared(new ReadOnlySpan<byte>(amountName));
                    raw.WriteInt(item.Amount);
                    raw.CloseObject();
                });
            }));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"amount\":12,\"amount\":12}", serializer.Serialize(new Money { Amount = 12 }));
        }

        [Fact]
        public void RawWriteApi_SupportsAllPrimitiveTypes()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareRawWriter<Money>((raw, item) =>
            {
                raw.OpenArray();
                raw.WriteUlong(ulong.MaxValue);
                raw.WriteComma();
                raw.WriteDecimal(1.5m);
                raw.WriteComma();
                raw.WriteFloat(2.5f);
                raw.WriteComma();
                raw.WriteUint(7u);
                raw.WriteComma();
                raw.WriteShort(-3);
                raw.WriteComma();
                raw.WriteUshort(9);
                raw.WriteComma();
                raw.WriteByte(255);
                raw.WriteComma();
                raw.WriteSbyte(-128);
                raw.CloseArray();
            })));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("[18446744073709551615,1.5,2.5,7,-3,9,255,-128]", serializer.Serialize(new Money()));
        }
    }
}
