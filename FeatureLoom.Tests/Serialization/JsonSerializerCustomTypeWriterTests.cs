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

        /// <summary>Two members of the same type, so a member scoped override can be isolated.</summary>
        private class Invoice
        {
            public Money Total;
            public Money Paid;
        }

        private class BaseItem
        {
            public int Code;
        }

        private class DerivedItem : BaseItem
        {
            public int Extra;
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

        /// <summary>
        /// Type info must be applied by the shape wrapper, not by the custom writer, so every shape
        /// has to produce the envelope the built-in writers produce for the same shape.
        /// </summary>
        [Fact]
        public void CustomValueWriter_WritesTypeInfoEnvelope()
        {
            var settings = TypeInfoSettings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareValueWriter<Money>((value, item) => value.WriteString(item.Currency))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"money\",\"$value\":\"EUR\"}", serializer.Serialize(new Money { Currency = "EUR" }));
        }

        [Fact]
        public void CustomObjectWriter_WritesTypeInfoAsFirstMember()
        {
            var settings = TypeInfoSettings();
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Person>(obj => obj
                .AddRawField("age", (raw, p) => raw.WriteInt(p.Age)))));
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeName("person"));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"person\",\"age\":42}", serializer.Serialize(new Person { Age = 42 }));
        }

        /// <summary>
        /// An empty body must not leave the comma that follows the type info behind.
        /// </summary>
        [Fact]
        public void CustomObjectWriter_WithoutFields_WritesOnlyTypeInfo()
        {
            var settings = TypeInfoSettings();
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<Person>(_ => { })));
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeName("person"));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"person\"}", serializer.Serialize(new Person { Name = "Ann" }));
        }

        [Fact]
        public void CustomArrayWriter_WritesTypeInfoEnvelope()
        {
            var settings = TypeInfoSettings();
            settings.ConfigureType<Tags>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareArrayWriter<Tags, string>(t => t.Values)));
            settings.ConfigureType<Tags>(ts => ts.SetCustomTypeName("tags"));
            settings.ConfigureType<string>(ts => ts.SetCustomTypeName("string"));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"tags\",\"$value\":[{\"$type\":\"string\",\"$value\":\"a\"}]}",
                serializer.Serialize(new Tags { Values = new List<string> { "a" } }));
        }

        [Fact]
        public void CustomRawWriter_WritesTypeInfoEnvelope()
        {
            var settings = TypeInfoSettings();
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareRawWriter<Money>((raw, item) =>
            {
                raw.OpenArray();
                raw.WriteInt(item.Amount);
                raw.CloseArray();
            })));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"money\",\"$value\":[12]}", serializer.Serialize(new Money { Amount = 12 }));
        }

        /// <summary>
        /// With AddDeviatingTypeInfo the envelope must appear only when the runtime type deviates
        /// from the declared one, which is the case the deserializer actually needs it for.
        /// </summary>
        [Fact]
        public void CustomWriter_WritesTypeInfoOnlyForDeviatingType()
        {
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddDeviatingTypeInfo
            };
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(
                prep => prep.PrepareValueWriter<BaseItem>((value, item) => value.WriteInt(item.Code)),
                true));
            settings.ConfigureType<DerivedItem>(ts => ts.SetCustomTypeName("derived"));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"Item\":1}", serializer.Serialize(new ItemHolder { Item = new BaseItem { Code = 1 } }));
            Assert.Equal("{\"Item\":{\"$type\":\"derived\",\"$value\":2}}",
                serializer.Serialize(new ItemHolder { Item = new DerivedItem { Code = 2 } }));
        }

        /// <summary>
        /// A per-type name set via ConfigureType must reach the custom writer's envelope too.
        /// </summary>
        [Fact]
        public void CustomWriter_UsesConfiguredTypeName()
        {
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<Money>(ts =>
            {
                ts.SetCustomTypeName("cash");
                ts.SetCustomTypeWriter(prep => prep.PrepareValueWriter<Money>((value, item) => value.WriteInt(item.Amount)));
            });

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"cash\",\"$value\":12}", serializer.Serialize(new Money { Amount = 12 }));
        }

        /// <summary>
        /// Mimicking a DTO: the writer suppresses the serializer's envelope for its own type and
        /// emits a "$type" naming a foreign type instead, so the JSON claims to be something the
        /// CLR type is not.
        /// </summary>
        [Fact]
        public void CustomWriter_CanEmitForeignTypeName()
        {
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<Money>(ts =>
            {
                ts.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
                ts.SetCustomTypeWriter(prep =>
                {
                    var typeInfo = prep.PrepareTypeInfo("MoneyDto");
                    byte[] amount = prep.PrepareFieldName("amount");
                    return prep.PrepareRawWriter<Money>((raw, item) =>
                    {
                        raw.OpenObject();
                        raw.WritePrepared(typeInfo);
                        raw.WriteComma();
                        raw.WritePrepared(amount);
                        raw.WriteInt(item.Amount);
                        raw.CloseObject();
                    });
                });
            });

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"MoneyDto\",\"amount\":12}", serializer.Serialize(new Money { Amount = 12 }));
        }

        /// <summary>
        /// The suppression must be local: the same type keeps its normal envelope in a serializer
        /// that does not configure the custom writer.
        /// </summary>
        [Fact]
        public void CustomWriter_ForeignTypeName_DoesNotLeakToOtherSerializers()
        {
            var settings = TypeInfoSettings();

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"money\",\"Amount\":12,\"Currency\":null}",
                serializer.Serialize<object>(new Money { Amount = 12 }));
        }

        /// <summary>
        /// The same trick scoped to a single member: only Money written as Order.Total claims the
        /// foreign type, while Money written anywhere else keeps its normal envelope.
        /// </summary>
        [Fact]
        public void CustomWriter_CanEmitForeignTypeName_ForSingleMember()
        {
            var settings = TypeInfoSettings();
            settings.ConfigureType<Invoice>(ts => ts.SetCustomTypeName("invoice"));
            settings.ConfigureType<Invoice>(ts => ts.ConfigureMember<Money>(nameof(Invoice.Total), ms =>
            {
                ms.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
                ms.SetCustomTypeWriter(prep =>
                {
                    byte[] typeInfo = prep.PrepareTypeInfo("MoneyDto");
                    byte[] amount = prep.PrepareFieldName("amount");
                    return prep.PrepareRawWriter<Money>((raw, item) =>
                    {
                        raw.OpenObject();
                        raw.WritePrepared(typeInfo);
                        raw.WriteComma();
                        raw.WritePrepared(amount);
                        raw.WriteInt(item.Amount);
                        raw.CloseObject();
                    });
                });
            }));

            var serializer = new JsonSerializer(settings);

            // Total is remapped, Paid keeps the normal envelope - same type, same object.
            Assert.Equal("{\"$type\":\"invoice\",\"Total\":{\"$type\":\"MoneyDto\",\"amount\":12}," +
                "\"Paid\":{\"$type\":\"money\",\"Amount\":7,\"Currency\":null}}",
                serializer.Serialize(new Invoice
                {
                    Total = new Money { Amount = 12 },
                    Paid = new Money { Amount = 7 }
                }));
        }

        /// <summary>
        /// The mimicked type need not exist in this process: the name is written verbatim, so a
        /// writer can target a type that lives only in the consuming system.
        /// </summary>
        [Fact]
        public void PrepareTypeInfo_AcceptsNameOfNonExistingType()
        {
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<Money>(ts =>
            {
                ts.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
                ts.SetCustomTypeWriter(prep =>
                {
                    byte[] typeInfo = prep.PrepareTypeInfo("Legacy.Money, LegacyApp");
                    return prep.PrepareRawWriter<Money>((raw, item) =>
                    {
                        raw.OpenObject();
                        raw.WritePrepared(typeInfo);
                        raw.CloseObject();
                    });
                });
            });

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"Legacy.Money, LegacyApp\"}", serializer.Serialize(new Money()));
        }

        /// <summary>
        /// SetCustomTypeName on a member scope applies only to that member, so the same CLR type
        /// can claim a different name depending on where it is written.
        /// </summary>
        [Fact]
        public void SetCustomTypeName_OnMemberScope_IsAppliedOnlyToThatMember()
        {
            var settings = TypeInfoSettings();
            settings.ConfigureType<Invoice>(ts => ts.SetCustomTypeName("invoice"));
            settings.ConfigureType<Invoice>(ts => ts.ConfigureMember<Money>(nameof(Invoice.Total),
                ms => ms.SetCustomTypeName("MoneyDto")));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"invoice\",\"Total\":{\"$type\":\"MoneyDto\",\"Amount\":12,\"Currency\":null}," +
                "\"Paid\":{\"$type\":\"money\",\"Amount\":7,\"Currency\":null}}",
                serializer.Serialize(new Invoice
                {
                    Total = new Money { Amount = 12 },
                    Paid = new Money { Amount = 7 }
                }));
        }

        /// <summary>
        /// PrepareTypeInfo must resolve the name the same way the serializer does, so a writer can
        /// emit the envelope of an existing type without hardcoding its name format.
        /// </summary>
        [Fact]
        public void PrepareTypeInfo_ResolvesConfiguredNameOfAnotherType()
        {
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<Person>(ts => ts.SetCustomTypeName("person"));
            settings.ConfigureType<Money>(ts =>
            {
                ts.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo);
                ts.SetCustomTypeWriter(prep =>
                {
                    var typeInfo = prep.PrepareTypeInfo<Person>();
                    return prep.PrepareRawWriter<Money>((raw, item) =>
                    {
                        raw.OpenObject();
                        raw.WritePrepared(typeInfo);
                        raw.CloseObject();
                    });
                });
            });

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"$type\":\"person\"}", serializer.Serialize(new Money()));
        }

        static JsonSerializer.Settings TypeInfoSettings()
        {
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddAllTypeInfo
            };
            settings.ConfigureType<Money>(ts => ts.SetCustomTypeName("money"));
            return settings;
        }

        [Fact]
        public void AddField_WithObjectDeclaredValue_WritesRuntimeType()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<BoxHolder>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<BoxHolder>(obj =>
                obj.AddField("value", h => h.Value))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"value\":{\"Code\":3}}", serializer.Serialize(new BoxHolder { Value = new BaseItem { Code = 3 } }));
            Assert.Equal("{\"value\":42}", serializer.Serialize(new BoxHolder { Value = 42 }));
            Assert.Equal("{\"value\":null}", serializer.Serialize(new BoxHolder { Value = null }));
        }

        [Fact]
        public void AddField_WithDerivedValue_WritesDerivedMembers()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<ItemHolder>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<ItemHolder>(obj =>
                obj.AddField("item", h => h.Item))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"item\":{\"Extra\":9,\"Code\":3}}",
                serializer.Serialize(new ItemHolder { Item = new DerivedItem { Code = 3, Extra = 9 } }));
        }

        /// <summary>
        /// A value written through the custom writer API must get the same deviating type info a
        /// built-in member would, otherwise it could not be read back as its original type.
        /// </summary>
        [Fact]
        public void AddField_WithDerivedValue_WritesDeviatingTypeInfo()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DerivedItem>(ts => ts.SetCustomTypeName("derived"));
            settings.ConfigureType<ItemHolder>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<ItemHolder>(obj =>
                obj.AddField("item", h => h.Item))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"item\":{\"$type\":\"derived\",\"Extra\":9,\"Code\":3}}",
                serializer.Serialize(new ItemHolder { Item = new DerivedItem { Code = 3, Extra = 9 } }));
        }

        [Fact]
        public void AddArray_WithObjectDeclaredItems_WritesRuntimeTypes()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<BoxListHolder>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<BoxListHolder>(obj =>
                obj.AddArray("values", h => h.Values))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"values\":[{\"Code\":3},7,null]}",
                serializer.Serialize(new BoxListHolder { Values = new List<object> { new BaseItem { Code = 3 }, 7, null } }));
        }

        [Fact]
        public void PrepareArrayWriter_WithObjectDeclaredItems_WritesRuntimeTypes()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<BoxListHolder>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareArrayWriter<BoxListHolder, object>(h => h.Values)));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("[{\"Code\":3},7]",
                serializer.Serialize(new BoxListHolder { Values = new List<object> { new BaseItem { Code = 3 }, 7 } }));
        }

        [Fact]
        public void PrepareTypeWriter_WithObjectDeclaredValue_WritesRuntimeType()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<BoxHolder>(ts => ts.SetCustomTypeWriter(prep =>
            {
                var writeValue = prep.PrepareTypeWriter<object>();
                return prep.PrepareRawWriter<BoxHolder>((raw, item) => writeValue(item.Value));
            }));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"Code\":3}", serializer.Serialize(new BoxHolder { Value = new BaseItem { Code = 3 } }));
        }

        /// <summary>
        /// Field local settings must stay in effect when the runtime type deviates, otherwise the
        /// override would silently stop applying for exactly the polymorphic values it targets.
        /// </summary>
        [Fact]
        public void AddField_WithDeviatingSettings_AppliesThemToRuntimeType()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<ItemHolder>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<ItemHolder>(obj =>
                obj.AddField("item", h => h.Item, s => s.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo)))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"item\":{\"Extra\":9,\"Code\":3}}",
                serializer.Serialize(new ItemHolder { Item = new DerivedItem { Code = 3, Extra = 9 } }));
            Assert.Equal("{\"item\":{\"Code\":3}}",
                serializer.Serialize(new ItemHolder { Item = new BaseItem { Code = 3 } }));
        }

        /// <summary>
        /// The field local settings must not leak into the shared per-type writer cache.
        /// </summary>
        [Fact]
        public void AddField_WithDeviatingSettings_DoesNotAffectOtherFields()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<TwoItemHolder>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<TwoItemHolder>(obj =>
                obj.AddField("plain", h => h.First)
                   .AddField("quiet", h => h.Second, s => s.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo)))));

            var serializer = new JsonSerializer(settings);
            var derived = new DerivedItem { Code = 3, Extra = 9 };
            string json = serializer.Serialize(new TwoItemHolder { First = derived, Second = derived });

            Assert.Contains("\"plain\":{\"$type\"", json);
            Assert.Contains("\"quiet\":{\"Extra\"", json);
        }

        [Fact]
        public void AddArray_WithDeviatingSettings_AppliesThemToRuntimeType()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<BoxListHolder>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<BoxListHolder>(obj =>
                obj.AddArray("values", h => h.Values, s => s.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddNoTypeInfo)))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"values\":[{\"Extra\":9,\"Code\":3},7]}",
                serializer.Serialize(new BoxListHolder { Values = new List<object> { new DerivedItem { Code = 3, Extra = 9 }, 7 } }));
        }

        /// <summary>
        /// A member override states only what it changes, so the settings configured for the type
        /// itself must survive it instead of being replaced wholesale.
        /// </summary>
        [Fact]
        public void MemberOverride_KeepsGeneralTypeSettings()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<BaseItem>(ts => ts.ConfigureMember<int>("Code", ms => ms.OverrideName("code")));
            settings.ConfigureType<ItemHolder>(ts => ts.ConfigureMember<BaseItem>("Item",
                ms => ms.SetTypeInfoHandling(JsonSerializer.TypeInfoHandling.AddAllTypeInfo)));

            var serializer = new JsonSerializer(settings);
            string json = serializer.Serialize(new ItemHolder { Item = new BaseItem { Code = 3 } });

            // The member override changes only the type info handling, so BaseItem's own member
            // configuration must still apply.
            Assert.Contains("\"code\":3", json);
            Assert.Contains("\"$type\"", json);
        }

        /// <summary>
        /// Merging the general member settings back in must not make writer creation recurse
        /// forever for a type that configures a member of its own type.
        /// </summary>
        [Fact]
        public void MemberOverride_OnSelfReferencingType_Terminates()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<Node>(ts => ts.ConfigureMember<Node>("Next",
                ms => ms.SetDataSelection(JsonSerializer.DataSelection.PublicFieldsAndProperties)));

            var serializer = new JsonSerializer(settings);
            var node = new Node { Code = 1, Next = new Node { Code = 2 } };

            Assert.Equal("{\"Code\":1,\"Next\":{\"Code\":2,\"Next\":null}}", serializer.Serialize(node));
        }

        /// <summary>
        /// A derived type inherits the members of its base type, so a member rule stated for the
        /// declared type must keep applying when the runtime type deviates.
        /// </summary>
        [Fact]
        public void MemberOverride_TransfersMemberSettingsToDerivedRuntimeType()
        {
            var settings = NoTypeInfoSettings();
            settings.ConfigureType<ItemHolder>(ts => ts.ConfigureMember<BaseItem>("Item",
                ms => ms.ConfigureMember<int>("Code", cs => cs.OverrideName("code"))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"Item\":{\"code\":3}}",
                serializer.Serialize(new ItemHolder { Item = new BaseItem { Code = 3 } }));
            // The inherited Code member must be renamed for the derived type as well.
            Assert.Equal("{\"Item\":{\"Extra\":9,\"code\":3}}",
                serializer.Serialize(new ItemHolder { Item = new DerivedItem { Code = 3, Extra = 9 } }));
        }

        /// <summary>
        /// A custom value writer may declare that its type contains no references, but that only
        /// holds for the declared type. A derived runtime value is written by a different writer,
        /// so the ref path bookkeeping must not be skipped for it.
        /// </summary>
        [Fact]
        public void ValueShapeWriter_OnUnsealedType_DoesNotDisableRefCheckForDerivedValues()
        {
            var settings = new JsonSerializer.Settings
            {
                typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo,
                referenceCheck = JsonSerializer.ReferenceCheck.AlwaysReplaceByRef
            };
            // The value writer states that a BaseItem has no children and therefore no reference
            // paths. BaseItem is not sealed, so a member declared as BaseItem may still hold a
            // DerivedItem at runtime, which is written as a full object by its own writer.
            settings.ConfigureType<BaseItem>(ts => ts.SetCustomTypeWriter(prep =>
                prep.PrepareValueWriter<BaseItem>((raw, item) => raw.WriteInt(item.Code))));

            var serializer = new JsonSerializer(settings);
            var shared = new DerivedItem { Code = 3, Extra = 9 };
            string json = serializer.Serialize(new SharedItemHolder { A = shared, B = shared });

            // Only possible if the ref path bookkeeping was kept enabled for these members.
            Assert.Contains("$ref", json);
        }

        static JsonSerializer.Settings NoTypeInfoSettings() => new JsonSerializer.Settings
        {
            typeInfoHandling = JsonSerializer.TypeInfoHandling.AddNoTypeInfo
        };

        private class Node
        {
            public int Code;
            public Node Next;
        }

        private class SharedItemHolder
        {
            public BaseItem A;
            public BaseItem B;
        }

        private class TwoItemHolder
        {
            public BaseItem First;
            public BaseItem Second;
        }

        private class BoxHolder
        {
            public object Value;
        }

        private class BoxListHolder
        {
            public List<object> Values;
        }

        private class ItemHolder
        {
            public BaseItem Item;
        }

        private class DynamicPerson
        {
            public string Name;
            public Dictionary<string, object> Extras = new();
        }

        [Fact]
        public void AddDynamicFields_WritesPropertiesNextToDeclaredFields()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DynamicPerson>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<DynamicPerson>(obj => obj
                .AddField("name", p => p.Name)
                .AddDynamicFields((dyn, p) =>
                {
                    foreach (var pair in p.Extras) dyn.WriteField(pair.Key, pair.Value);
                }))));

            var serializer = new JsonSerializer(settings);
            var person = new DynamicPerson { Name = "Ann", Extras = { ["age"] = 42, ["city"] = "Berlin" } };

            Assert.Equal("{\"name\":\"Ann\",\"age\":42,\"city\":\"Berlin\"}", serializer.Serialize(person));
        }

        /// <summary>
        /// Writing no dynamic property at all must not leave a dangling comma behind.
        /// </summary>
        [Fact]
        public void AddDynamicFields_WithNoProperties_WritesNoDanglingComma()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DynamicPerson>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<DynamicPerson>(obj => obj
                .AddField("name", p => p.Name)
                .AddDynamicFields((dyn, p) =>
                {
                    foreach (var pair in p.Extras) dyn.WriteField(pair.Key, pair.Value);
                }))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"name\":\"Ann\"}", serializer.Serialize(new DynamicPerson { Name = "Ann" }));
        }

        /// <summary>
        /// A declared field following a dynamic one must decide about its comma at runtime,
        /// because it cannot know whether the dynamic field wrote anything.
        /// </summary>
        [Fact]
        public void AddDynamicFields_FollowedByDeclaredField_SeparatesCorrectly()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DynamicPerson>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<DynamicPerson>(obj => obj
                .AddDynamicFields((dyn, p) =>
                {
                    foreach (var pair in p.Extras) dyn.WriteField(pair.Key, pair.Value);
                })
                .AddField("name", p => p.Name))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"name\":\"Ann\"}", serializer.Serialize(new DynamicPerson { Name = "Ann" }));
            Assert.Equal("{\"age\":42,\"name\":\"Ann\"}",
                serializer.Serialize(new DynamicPerson { Name = "Ann", Extras = { ["age"] = 42 } }));
        }

        [Fact]
        public void AddDynamicFields_WritesValuesWithTheirRuntimeType()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DynamicPerson>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<DynamicPerson>(obj => obj
                .AddDynamicFields((dyn, p) =>
                {
                    foreach (var pair in p.Extras) dyn.WriteField(pair.Key, pair.Value);
                }))));

            var serializer = new JsonSerializer(settings);
            var person = new DynamicPerson
            {
                Extras =
                {
                    ["nested"] = new Person { Name = "Bob", Age = 7 },
                    ["missing"] = null
                }
            };

            Assert.Equal("{\"nested\":{\"Name\":\"Bob\",\"Age\":7},\"missing\":null}", serializer.Serialize(person));
        }

        [Fact]
        public void AddDynamicObject_WritesNestedObjectWithDynamicProperties()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DynamicPerson>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<DynamicPerson>(obj => obj
                .AddField("name", p => p.Name)
                .AddDynamicObject("extras", p => p.Extras, (dyn, extras) =>
                {
                    foreach (var pair in extras) dyn.WriteField(pair.Key, pair.Value);
                }))));

            var serializer = new JsonSerializer(settings);
            var person = new DynamicPerson { Name = "Ann", Extras = { ["age"] = 42 } };

            Assert.Equal("{\"name\":\"Ann\",\"extras\":{\"age\":42}}", serializer.Serialize(person));
        }

        [Fact]
        public void AddDynamicObject_WithNoProperties_WritesEmptyObject()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DynamicPerson>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<DynamicPerson>(obj => obj
                .AddDynamicObject("extras", p => p.Extras, (dyn, extras) =>
                {
                    foreach (var pair in extras) dyn.WriteField(pair.Key, pair.Value);
                }))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"extras\":{}}", serializer.Serialize(new DynamicPerson { Name = "Ann" }));
        }

        [Fact]
        public void AddDynamicObject_WithNullValue_WritesNull()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<DynamicPerson>(ts => ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<DynamicPerson>(obj => obj
                .AddDynamicObject("extras", p => p.Extras, (dyn, extras) =>
                {
                    foreach (var pair in extras) dyn.WriteField(pair.Key, pair.Value);
                }))));

            var serializer = new JsonSerializer(settings);

            Assert.Equal("{\"extras\":null}", serializer.Serialize(new DynamicPerson { Name = "Ann", Extras = null }));
        }

        private class ExtendablePerson
        {
            public string Name;
            public int Age;
            public string Secret;
            [JsonIgnore] public string Internal;
            public Dictionary<string, object> Extras = new();
        }

        [Fact]
        public void AddExistingFields_WritesDefaultMembers()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<ExtendablePerson>(ts =>
                ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<ExtendablePerson>(obj => obj
                    .AddExistingFields())));

            var serializer = new JsonSerializer(settings);
            var person = new ExtendablePerson { Name = "Ann", Age = 42, Secret = "s", Internal = "i", Extras = null };

            Assert.Equal("{\"Name\":\"Ann\",\"Age\":42,\"Secret\":\"s\",\"Extras\":null}", serializer.Serialize(person));
        }

        /// <summary>
        /// The member configuration must be honored the same way as without a custom writer.
        /// </summary>
        [Fact]
        public void AddExistingFields_HonorsMemberSettings()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<ExtendablePerson>(ts =>
            {
                ts.ConfigureMember<string>(nameof(ExtendablePerson.Secret), ms => ms.SetIgnore());
                ts.ConfigureMember<Dictionary<string, object>>(nameof(ExtendablePerson.Extras), ms => ms.SetIgnore());
                ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<ExtendablePerson>(obj => obj
                    .AddExistingFields()));
            });

            var serializer = new JsonSerializer(settings);
            var person = new ExtendablePerson { Name = "Ann", Age = 42, Secret = "s" };

            Assert.Equal("{\"Name\":\"Ann\",\"Age\":42}", serializer.Serialize(person));
        }

        [Fact]
        public void AddExistingFields_CanBeExtendedByFurtherFields()
        {
            var settings = new JsonSerializer.Settings();
            settings.ConfigureType<ExtendablePerson>(ts =>
            {
                ts.ConfigureMember<string>(nameof(ExtendablePerson.Secret), ms => ms.SetIgnore());
                ts.ConfigureMember<Dictionary<string, object>>(nameof(ExtendablePerson.Extras), ms => ms.SetIgnore());
                ts.SetCustomTypeWriter(prep => prep.PrepareObjectWriter<ExtendablePerson>(obj => obj
                    .AddField("greeting", p => "Hi " + p.Name)
                    .AddExistingFields()
                    .AddDynamicFields((dyn, p) =>
                    {
                        foreach (var pair in p.Extras) dyn.WriteField(pair.Key, pair.Value);
                    })
                    .AddField("tag", p => "end")));
            });

            var serializer = new JsonSerializer(settings);
            var person = new ExtendablePerson { Name = "Ann", Age = 42, Extras = { ["city"] = "Berlin" } };

            Assert.Equal("{\"greeting\":\"Hi Ann\",\"Name\":\"Ann\",\"Age\":42,\"city\":\"Berlin\",\"tag\":\"end\"}",
                serializer.Serialize(person));
        }
    }
}
