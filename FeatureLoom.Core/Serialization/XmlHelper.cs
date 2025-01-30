using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Schema;
using System.Xml;
using System.Xml.Serialization;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

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

    public static bool TryCreateXmlSchema(Type type, out XmlSchema schema, out string xsd)
    {
        schema = null;
        xsd = null;
        try
        {
            XmlSchemas xmlSchemas = new XmlSchemas();
            XmlSchemaExporter exporter = new XmlSchemaExporter(xmlSchemas);
            XmlReflectionImporter importer = new XmlReflectionImporter();
            XmlTypeMapping mapping = importer.ImportTypeMapping(type);

            exporter.ExportTypeMapping(mapping);

            // Assume we want only the first (and primary) schema, which contains nested types
            schema = xmlSchemas.Count > 0 ? xmlSchemas[0] : null;
            if (schema == null) return false;

            // Ensure required and optional elements are correctly set
            foreach (XmlSchemaElement element in schema.Items.OfType<XmlSchemaElement>())
            {
                var prop = type.GetProperty(element.Name);
                if (prop != null)
                {
                    var xmlElementAttr = prop.GetCustomAttribute<XmlElementAttribute>();
                    bool isRequired = xmlElementAttr != null && !xmlElementAttr.IsNullable;
                    bool hasDefault = prop.GetCustomAttribute<DefaultValueAttribute>() != null;

                    if (isRequired)
                    {
                        element.MinOccurs = 1; // Make it mandatory
                    }
                    else if (hasDefault)
                    {
                        element.MinOccurs = 0; // Make it optional
                    }
                }
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
