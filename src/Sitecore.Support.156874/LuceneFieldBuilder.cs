namespace Sitecore.Support.ContentSearch.LuceneProvider
{
  using Lucene.Net.Documents;
  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Linq.Parsing;
  using Sitecore.ContentSearch.LuceneProvider;
  using Sitecore.ContentSearch.Utilities;
  using System;
  using System.Globalization;
  using System.Text;

  public static class LuceneFieldBuilder
  {
    public static IFieldable CreateField(string name, object value, LuceneSearchFieldConfiguration fieldConfiguration, IIndexValueFormatter indexFieldStorageValueFormatter)
    {
      Func<string> func4 = null;
      Func<string> func5 = null;
      Func<string> func6 = null;
      Func<string> messageDelegate = null;
      Func<string> func2 = null;
      Func<string> func3 = null;
      if (value == null)
      {
        if (messageDelegate == null)
        {
          if (func4 == null)
          {
            func4 = () => $"Skipping field {name} - value null";
          }
          messageDelegate = func4;
        }
        VerboseLogging.CrawlingLogDebug(messageDelegate);
        return null;
      }
      if (fieldConfiguration == null)
      {
        throw new ArgumentNullException("fieldConfiguration");
      }
      if (VerboseLogging.Enabled)
      {
        StringBuilder builder = new StringBuilder();
        builder.AppendFormat("Field: {0}" + Environment.NewLine, name);
        builder.AppendFormat(" - value: {0}" + Environment.NewLine, value.GetType());
        builder.AppendFormat(" - value: {0}" + Environment.NewLine, value);
        builder.AppendFormat(" - fieldConfiguration analyzer: {0}" + Environment.NewLine, (fieldConfiguration.Analyzer != null) ? fieldConfiguration.Analyzer.GetType().ToString() : "NULL");
        builder.AppendFormat(" - fieldConfiguration boost: {0}" + Environment.NewLine, fieldConfiguration.Boost);
        builder.AppendFormat(" - fieldConfiguration fieldID: {0}" + Environment.NewLine, fieldConfiguration.FieldID);
        builder.AppendFormat(" - fieldConfiguration FieldName: {0}" + Environment.NewLine, fieldConfiguration.FieldName);
        builder.AppendFormat(" - fieldConfiguration FieldTypeName: {0}" + Environment.NewLine, fieldConfiguration.FieldTypeName);
        builder.AppendFormat(" - fieldConfiguration IndexType: {0}" + Environment.NewLine, fieldConfiguration.IndexType);
        builder.AppendFormat(" - fieldConfiguration StorageType: {0}" + Environment.NewLine, fieldConfiguration.StorageType);
        builder.AppendFormat(" - fieldConfiguration VectorType: {0}" + Environment.NewLine, fieldConfiguration.VectorType);
        builder.AppendFormat(" - fieldConfiguration Type: {0}" + Environment.NewLine, fieldConfiguration.Type);
        VerboseLogging.CrawlingLogDebug(new Func<string>(builder.ToString));
      }
      if (IsNumericField(fieldConfiguration.Type))
      {
        long num;
        if ((value is string) && string.IsNullOrEmpty((string)value))
        {
          if (func2 == null)
          {
            if (func5 == null)
            {
              func5 = () => $"Skipping field {name} - value or empty null";
            }
            func2 = func5;
          }
          VerboseLogging.CrawlingLogDebug(func2);
          return null;
        }
        if (long.TryParse(value.ToString(), out num))
        {
          NumericField field = new NumericField(name, fieldConfiguration.StorageType, fieldConfiguration.IndexType == Field.Index.ANALYZED);
          field.SetLongValue((long)Convert.ChangeType(value, typeof(long)));
          return field;
        }
      }
      if (IsFloatingPointField(fieldConfiguration.Type))
      {
        if ((value is string) && string.IsNullOrEmpty((string)value))
        {
          if (func3 == null)
          {
            if (func6 == null)
            {
              func6 = () => $"Skipping field {name} - value or empty null";
            }
            func3 = func6;
          }
          VerboseLogging.CrawlingLogDebug(func3);
          return null;
        }
        NumericField field2 = new NumericField(name, 4, fieldConfiguration.StorageType, fieldConfiguration.IndexType == Field.Index.ANALYZED);
        var language = ContentSearchManager.Locator.GetInstance<CultureWrapper>();
        // Patch 96353
        field2.SetDoubleValue((double)Convert.ChangeType(value.ToString().Replace(',', '.'), typeof(double), CultureInfo.InvariantCulture));
        return field2;
      }
      string str = indexFieldStorageValueFormatter.FormatValueForIndexStorage(value, name).ToString();
      if (VerboseLogging.Enabled)
      {
        StringBuilder builder2 = new StringBuilder();
        builder2.AppendFormat("Field: {0}" + Environment.NewLine, name);
        builder2.AppendFormat(" - formattedValue: {0}" + Environment.NewLine, str);
        VerboseLogging.CrawlingLogDebug(new Func<string>(builder2.ToString));
      }
      return new Field(name, str, fieldConfiguration.StorageType, fieldConfiguration.IndexType, fieldConfiguration.VectorType);
    }

    public static bool IsFloatingPointField(Type type)
    {
      if (!type.IsAssignableFrom(typeof(double)))
      {
        return type.IsAssignableFrom(typeof(float));
      }
      return true;
    }

    public static bool IsNumericField(Type type)
    {
      if (type == null)
      {
        throw new ArgumentNullException("type");
      }
      if (!type.IsAssignableFrom(typeof(int)))
      {
        if (type.IsAssignableFrom(typeof(uint)))
        {
          return true;
        }
        if (type.IsAssignableFrom(typeof(short)))
        {
          return true;
        }
        if (type.IsAssignableFrom(typeof(ushort)))
        {
          return true;
        }
        if (type.IsAssignableFrom(typeof(long)))
        {
          return true;
        }
        if (type.IsAssignableFrom(typeof(ulong)))
        {
          return true;
        }
        if (!type.IsAssignableFrom(typeof(byte)))
        {
          return type.IsAssignableFrom(typeof(sbyte));
        }
      }
      return true;
    }
  }
}
