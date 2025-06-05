using FeatureLoom.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace FeatureLoom.Serialization;

public static class XmlHelper
{
    public static XmlElement ToXmlElement(string xml, XmlDocument xmlDoc = null)
    {
        if (xmlDoc == null) xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);
        return xmlDoc.DocumentElement;
    }

    public static bool TryDeserializeXml<T>(Stream stream, out T xmlObject, Encoding encoding = null)
    {
        try
        {
            encoding ??= Encoding.UTF8;
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StreamReader streamReader = new StreamReader(stream, encoding))
            using (XmlReader reader = XmlReader.Create(streamReader))
            {
                xmlObject = (T)serializer.Deserialize(reader);
            }

            return true;
        }
        catch
        {
            xmlObject = default;
            return false;
        }
    }

    public static bool TryDeserializeXml<T>(string xmlString, out T xmlObject)
    {
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringReader stringReader = new StringReader(xmlString))
            using (XmlReader reader = XmlReader.Create(stringReader))
            {
                xmlObject = (T)serializer.Deserialize(reader);
            }

            return true;
        }
        catch
        {
            xmlObject = default;
            return false;
        }
    }

    public static bool TryExtractXmlNode(string xmlString, string xPath, out string xmlNode)
    {               
        if (TryExtractXmlNode(xmlString, xPath, out XmlNode node))
        {
            xmlNode = node.OuterXml;
            return true;
        }   
        
        xmlNode = null;
        return false;
    }

    public static bool TryExtractXmlNode(string xmlString, string xPath, out XmlNode xmlNode)
    {
        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xmlString);
            xmlNode = doc.SelectSingleNode(xPath);
            return xmlNode != null;
        }
        catch
        {
            xmlNode = null;
            return false;
        }
    }

    public static bool TryDeserializeXmlElement<T>(string xmlString, string xPath, out T xmlObject)
    {
        try
        {
            xmlObject = default!;
            if (!TryExtractXmlNode(xmlString, xPath, out string targetXml)) return false;

            // Deserialize the target node
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (StringReader stringReader = new StringReader(targetXml))
            using (XmlReader reader = XmlReader.Create(stringReader))
            {
                xmlObject = (T)serializer.Deserialize(reader)!;
            }

            return true;
        }
        catch
        {
            xmlObject = default!;
            return false;
        }
    }

    public static bool TrySerializeToXmlElement<T>(T obj, out XmlElement xmlElement)
    {
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            XmlDocument doc = new XmlDocument();

            using (XmlWriter writer = doc.CreateNavigator().AppendChild())
            {
                serializer.Serialize(writer, obj);
            }

            xmlElement = doc.DocumentElement;
            return true;
        }
        catch
        {
            xmlElement = null;
            return false;
        }
    }

    public static bool TrySerializeToXmlString<T>(T obj, out string xmlString, bool omitXmlDeclaration = false)
    {
        try
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineOnAttributes = false,
                OmitXmlDeclaration = omitXmlDeclaration
            };

            using (var stringWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
                {
                    serializer.Serialize(xmlWriter, obj);
                }

                xmlString = stringWriter.ToString();
            }

            return true;
        }
        catch
        {
            xmlString = null;
            return false;
        }
    }

    public static bool TryCreateXmlSchema(Type rootType, out XmlSchema schema, out string xsd)
    {
        schema = null;
        xsd = null;
        try
        {
            XmlSchemas xmlSchemas = new XmlSchemas();
            XmlSchemaExporter exporter = new XmlSchemaExporter(xmlSchemas);
            XmlReflectionImporter importer = new XmlReflectionImporter();
            XmlTypeMapping mapping = importer.ImportTypeMapping(rootType);

            exporter.ExportTypeMapping(mapping);

            // Assume we want only the first (and primary) schema, which contains nested types
            schema = xmlSchemas.Count > 0 ? xmlSchemas[0] : null;
            if (schema == null) return false;

            // Collect all CLR types in object graph (props + fields)
            var allTypes = new HashSet<Type>();
            void Collect(Type t)
            {
                if (t == null || t == typeof(string) || t.IsPrimitive)
                    return;
                if (!allTypes.Add(t))
                    return;

                // PROPERTIES
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                   .Where(p => p.GetCustomAttribute<XmlIgnoreAttribute>() == null))
                {
                    var pt = p.PropertyType;

                    // Also scan XmlArrayItem attributes for concrete types
                    foreach (var arrayItem in p.GetCustomAttributes<XmlArrayItemAttribute>())
                        if (arrayItem.Type != null)
                            Collect(arrayItem.Type);

                    if (pt.IsArray) pt = pt.GetElementType();
                    else if (pt.IsGenericType &&
                             pt.GetInterfaces().Any(i =>
                                 i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                        pt = pt.GetGenericArguments()[0];

                    Collect(pt);
                }

                // FIELDS
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                   .Where(f => f.GetCustomAttribute<XmlIgnoreAttribute>() == null))
                {
                    var ft = f.FieldType;

                    // Also scan XmlArrayItem attributes for concrete types
                    foreach (var arrayItem in f.GetCustomAttributes<XmlArrayItemAttribute>())
                        if (arrayItem.Type != null)
                            Collect(arrayItem.Type);

                    if (ft.IsArray) ft = ft.GetElementType();
                    else if (ft.IsGenericType &&
                             ft.GetInterfaces().Any(i =>
                                 i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
                        ft = ft.GetGenericArguments()[0];

                    Collect(ft);
                }
            }
            Collect(rootType);

            // map XML typeName -> CLR type
            var nameMap = allTypes.ToDictionary(
                t => {
                    var xt = t.GetCustomAttribute<XmlTypeAttribute>();
                    return !string.IsNullOrEmpty(xt?.TypeName)
                         ? xt.TypeName
                         : t.Name;
                },
                t => t);

            // Patch each complexType for [XmlText] or [XmlAttribute] on props + fields
            foreach (XmlSchemaComplexType ct in schema.Items.OfType<XmlSchemaComplexType>())
            {
                if (string.IsNullOrEmpty(ct.Name))
                    continue;
                if (!nameMap.TryGetValue(ct.Name, out var clrType))
                    continue;

                // find XmlText on property or field
                var textMember = clrType
                  .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                  .FirstOrDefault(m => m.GetCustomAttribute<XmlTextAttribute>() != null);
                if (textMember == null)
                    continue;

                // build simpleContent/extension(base=xs:string)
                var simple = new XmlSchemaSimpleContent();
                var ext = new XmlSchemaSimpleContentExtension
                {
                    BaseTypeName = new XmlQualifiedName("string", XmlSchema.Namespace)
                };

                // carry over any XmlAttribute on props or fields
                var attributeMembers = clrType
                    .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttribute<XmlAttributeAttribute>() != null);

                foreach (var member in attributeMembers)
                {
                    var xa = member.GetCustomAttribute<XmlAttributeAttribute>();
                    Type memberType = (member is PropertyInfo pi)
                        ? pi.PropertyType
                        : ((FieldInfo)member).FieldType;

                    var schemaAttr = new XmlSchemaAttribute
                    {
                        Name = xa.AttributeName.EmptyOrNull() ? member.Name : xa.AttributeName,
                        SchemaTypeName = GetXsdTypeName(memberType)
                    };
                    ext.Attributes.Add(schemaAttr);
                }

                simple.Content = ext;
                ct.ContentModel = simple;
                ct.Particle = null;  // drop previous sequence
            }



            using (StringWriter stringWriter = new StringWriter())
            using (XmlTextWriter xmlWriter = new XmlTextWriter(stringWriter))
            {
                schema?.Write(xmlWriter);
                xsd = stringWriter.ToString();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Map CLR types to the corresponding XSD built‑in simple type QName.
    /// Extend this dictionary as you need more mappings.
    /// </summary>
    private static XmlQualifiedName GetXsdTypeName(Type clrType)
    {
        var ns = XmlSchema.Namespace;  // "http://www.w3.org/2001/XMLSchema"
        if (clrType == typeof(string)) return new XmlQualifiedName("string", ns);
        if (clrType == typeof(int)) return new XmlQualifiedName("int", ns);
        if (clrType == typeof(bool)) return new XmlQualifiedName("boolean", ns);
        if (clrType == typeof(decimal)) return new XmlQualifiedName("decimal", ns);
        if (clrType == typeof(double)) return new XmlQualifiedName("double", ns);
        if (clrType == typeof(float)) return new XmlQualifiedName("float", ns);
        if (clrType == typeof(long)) return new XmlQualifiedName("long", ns);
        if (clrType == typeof(short)) return new XmlQualifiedName("short", ns);
        if (clrType == typeof(byte)) return new XmlQualifiedName("byte", ns);
        if (clrType == typeof(DateTime)) return new XmlQualifiedName("dateTime", ns);

        // fallback
        return new XmlQualifiedName("string", ns);
    }

    public static bool ValidateXml(this string xmlString, XmlSchema schema, out string[] errors)
    {
        List<string> errorList = new List<string>();

        XmlReaderSettings settings = new XmlReaderSettings();
        settings.Schemas.Add(schema);
        settings.ValidationType = ValidationType.Schema;
        settings.ValidationEventHandler += (sender, e) =>
        {
            errorList.Add(e.Message);
        };

        using (StringReader stringReader = new StringReader(xmlString))
        using (XmlReader reader = XmlReader.Create(stringReader, settings))
        {
            try
            {
                // Read the XML document and trigger validation
                while (reader.Read()) { }
            }
            catch (XmlException ex)
            {
                errorList.Add($"XML Parsing error: {ex.Message}");
            }
        }

        if (errorList.Count > 0)
        {
            errors = errorList.ToArray();
            return false;
        }
        else
        {
            errors = Array.Empty<string>();
            return true;
        }
    }

}
