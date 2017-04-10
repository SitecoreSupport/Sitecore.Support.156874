namespace Sitecore.Support.ContentSearch.LuceneProvider
{
  using Lucene.Net.Documents;
  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Boosting;
  using Sitecore.ContentSearch.ComputedFields;
  using Sitecore.ContentSearch.Diagnostics;
  using Sitecore.ContentSearch.LuceneProvider;
  using Sitecore.Diagnostics;
  using System;
  using System.Collections;
  using System.Collections.Concurrent;
  using System.Reflection;
  using System.Runtime.InteropServices;
  using System.Text;
  using System.Threading.Tasks;

  public class LuceneDocumentBuilder : Sitecore.ContentSearch.LuceneProvider.LuceneDocumentBuilder
  {
    private ConcurrentQueue<IFieldable> _fields;
    private readonly IProviderUpdateContext Context;
    private readonly LuceneSearchFieldConfiguration defaultStoreField;
    private readonly LuceneSearchFieldConfiguration defaultTextField;

    public LuceneDocumentBuilder(IIndexable indexable, IProviderUpdateContext context) : base(indexable, context)
    {
      this.defaultTextField = new LuceneSearchFieldConfiguration("NO", "TOKENIZED", "NO", 1f);
      this.defaultStoreField = new LuceneSearchFieldConfiguration("NO", "TOKENIZED", "YES", 1f);
      this.Context = context;
      this._fields = base.GetType().BaseType.GetField("fields", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(this) as ConcurrentQueue<IFieldable>;
    }

    public override void AddBoost()
    {
      float num = BoostingManager.ResolveItemBoosting(base.Indexable);
      if (num > 0f)
      {
        base.Document.Boost = num;
      }
    }

    private void AddComputedIndexField(IComputedIndexField computedIndexField, object fieldValue)
    {
      LuceneSearchFieldConfiguration fieldConfiguration = base.Index.Configuration.FieldMap.GetFieldConfiguration(computedIndexField.FieldName) as LuceneSearchFieldConfiguration;
      if ((fieldValue is IEnumerable) && !(fieldValue is string))
      {
        foreach (object obj2 in fieldValue as IEnumerable)
        {
          if (fieldConfiguration != null)
          {
            this.AddField(computedIndexField.FieldName, obj2, fieldConfiguration, 0f);
          }
          else
          {
            this.AddField(computedIndexField.FieldName, obj2, false);
          }
        }
      }
      else if (fieldConfiguration != null)
      {
        this.AddField(computedIndexField.FieldName, fieldValue, fieldConfiguration, 0f);
      }
      else
      {
        this.AddField(computedIndexField.FieldName, fieldValue, false);
      }
    }

    public override void AddComputedIndexFields()
    {
      try
      {
        VerboseLogging.CrawlingLogDebug(() => "AddComputedIndexFields Start");
        if (base.IsParallel)
        {
          ConcurrentQueue<Exception> exceptions = new ConcurrentQueue<Exception>();
          Parallel.ForEach<IComputedIndexField>(base.Options.ComputedIndexFields, base.ParallelOptions, delegate (IComputedIndexField computedIndexField, ParallelLoopState parallelLoopState) {
            object obj2;
            try
            {
              obj2 = computedIndexField.ComputeFieldValue(this.Indexable);
            }
            catch (Exception exception)
            {
              CrawlingLog.Log.Warn($"Could not compute value for ComputedIndexField: {computedIndexField.FieldName} for indexable: {this.Indexable.UniqueId}", exception);
              if (this.Settings.StopOnCrawlFieldError())
              {
                exceptions.Enqueue(exception);
                parallelLoopState.Stop();
              }
              return;
            }
            this.AddComputedIndexField(computedIndexField, obj2);
          });
          if (exceptions.Count > 0)
          {
            throw new AggregateException(exceptions);
          }
        }
        else
        {
          foreach (IComputedIndexField field in base.Options.ComputedIndexFields)
          {
            object obj2;
            try
            {
              obj2 = field.ComputeFieldValue(base.Indexable);
            }
            catch (Exception exception)
            {
              CrawlingLog.Log.Warn($"Could not compute value for ComputedIndexField: {field.FieldName} for indexable: {base.Indexable.UniqueId}", exception);
              if (base.Settings.StopOnCrawlFieldError())
              {
                throw;
              }
              continue;
            }
            this.AddComputedIndexField(field, obj2);
          }
        }
      }
      finally
      {
        VerboseLogging.CrawlingLogDebug(() => "AddComputedIndexFields End");
      }
    }

    public override void AddField(IIndexableDataField field)
    {
      Func<string> func2 = null;
      Func<string> messageDelegate = null;
      AbstractSearchFieldConfiguration fieldConfiguration = this.Context.Index.Configuration.FieldMap.GetFieldConfiguration(field);
      object fieldValue = base.Index.Configuration.FieldReaders.GetFieldValue(field);
      string name = field.Name;
      LuceneSearchFieldConfiguration fieldSettings = base.Index.Configuration.FieldMap.GetFieldConfiguration(field) as LuceneSearchFieldConfiguration;
      if (fieldSettings == null)
      {
        if (messageDelegate == null)
        {
          if (func2 == null)
          {
            func2 = () => $"Cannot resolve field settings for field id:{field.Id}, name:{field.Name}, typeKey:{field.TypeKey} - The field will not be added to the index.";
          }
          messageDelegate = func2;
        }
        VerboseLogging.CrawlingLogDebug(messageDelegate);
      }
      else
      {
        fieldValue = fieldConfiguration.FormatForWriting(fieldValue);
        float boost = BoostingManager.ResolveFieldBoosting(field);
        if (IndexOperationsHelper.IsTextField(field))
        {
          LuceneSearchFieldConfiguration configuration3 = base.Index.Configuration.FieldMap.GetFieldConfiguration("_content") as LuceneSearchFieldConfiguration;
          this.AddField("_content", fieldValue, configuration3 ?? this.defaultTextField, 0f);
        }
        this.AddField(name, fieldValue, fieldSettings, boost);
      }
    }

    public override void AddField(string fieldName, object fieldValue, bool append = false)
    {
      AbstractSearchFieldConfiguration fieldConfiguration = this.Context.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName);
      string str = fieldName;
      fieldName = base.Index.FieldNameTranslator.GetIndexFieldName(fieldName);
      LuceneSearchFieldConfiguration fieldSettings = base.Index.Configuration.FieldMap.GetFieldConfiguration(fieldName) as LuceneSearchFieldConfiguration;
      if (fieldSettings != null)
      {
        if (fieldConfiguration != null)
        {
          fieldValue = fieldConfiguration.FormatForWriting(fieldValue);
        }
        this.AddField(fieldName, fieldValue, fieldSettings, 0f);
      }
      else
      {
        object obj2;
        if (VerboseLogging.Enabled)
        {
          StringBuilder builder = new StringBuilder();
          builder.AppendFormat("Field: {0} (Adding field with no field configuration)" + Environment.NewLine, fieldName);
          builder.AppendFormat(" - value: {0}" + Environment.NewLine, (fieldValue != null) ? fieldValue.GetType().ToString() : "NULL");
          builder.AppendFormat(" - value: {0}" + Environment.NewLine, fieldValue);
          VerboseLogging.CrawlingLogDebug(new Func<string>(builder.ToString));
        }
        IEnumerable enumerable = fieldValue as IEnumerable;
        if ((enumerable != null) && !(fieldValue is string))
        {
          foreach (object obj3 in enumerable)
          {
            obj2 = base.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(obj3, str);
            if (fieldConfiguration != null)
            {
              obj2 = fieldConfiguration.FormatForWriting(obj2);
            }
            if (obj2 != null)
            {
              this._fields.Enqueue(new Field(fieldName, obj2.ToString(), Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
            }
          }
        }
        else
        {
          obj2 = base.Index.Configuration.IndexFieldStorageValueFormatter.FormatValueForIndexStorage(fieldValue, str);
          if (fieldConfiguration != null)
          {
            obj2 = fieldConfiguration.FormatForWriting(obj2);
          }
          if (obj2 != null)
          {
            this._fields.Enqueue(new Field(fieldName, obj2.ToString(), Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.WITH_POSITIONS_OFFSETS));
          }
        }
      }
    }

    protected void AddField(string name, object value, LuceneSearchFieldConfiguration fieldSettings, float boost = 0f)
    {
      IFieldable fieldable;
      Assert.IsNotNull(fieldSettings, "fieldSettings");
      name = base.Index.FieldNameTranslator.GetIndexFieldName(name);
      boost += fieldSettings.Boost;
      IEnumerable enumerable = value as IEnumerable;
      if ((enumerable != null) && value.GetType().IsGenericType)
      {
        foreach (object obj2 in enumerable)
        {
          object obj3 = fieldSettings.FormatForWriting(obj2);
          fieldable = Sitecore.Support.ContentSearch.LuceneProvider.LuceneFieldBuilder.CreateField(name, obj3, fieldSettings, base.Index.Configuration.IndexFieldStorageValueFormatter);
          if (fieldable != null)
          {
            fieldable.Boost = boost;
            this._fields.Enqueue(fieldable);
          }
        }
      }
      else
      {
        value = fieldSettings.FormatForWriting(value);
        fieldable = Sitecore.Support.ContentSearch.LuceneProvider.LuceneFieldBuilder.CreateField(name, value, fieldSettings, base.Index.Configuration.IndexFieldStorageValueFormatter);
        if (fieldable != null)
        {
          fieldable.Boost = boost;
          this._fields.Enqueue(fieldable);
        }
      }
    }

    public ConcurrentQueue<IFieldable> CollectedFields =>
        this._fields;
  }
}
