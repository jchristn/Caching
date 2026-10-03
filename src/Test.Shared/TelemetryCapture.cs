namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;

    using Caching;

    /// <summary>
    /// In-memory listener for the Caching meter and activity source, scoped to a single cache name so tests
    /// running in parallel do not see each other's telemetry.
    /// </summary>
    public sealed class TelemetryCapture : IDisposable
    {
        private readonly string _CacheName;
        private readonly MeterListener _MeterListener;
        private readonly ActivityListener _ActivityListener;
        private readonly ConcurrentQueue<CapturedMeasurement> _Measurements = new ConcurrentQueue<CapturedMeasurement>();
        private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();

        /// <summary>
        /// Start capturing telemetry for caches whose Name equals cacheName.
        /// </summary>
        /// <param name="cacheName">Cache name to filter on.</param>
        /// <param name="captureMetrics">Subscribe to metrics.</param>
        /// <param name="captureTraces">Subscribe to traces.</param>
        public TelemetryCapture(string cacheName, bool captureMetrics = true, bool captureTraces = true)
        {
            _CacheName = cacheName ?? throw new ArgumentNullException(nameof(cacheName));

            if (captureMetrics)
            {
                _MeterListener = new MeterListener();
                _MeterListener.InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == CacheTelemetryNames.MeterName)
                        listener.EnableMeasurementEvents(instrument);
                };
                _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
                _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => OnMeasurement(instrument, value, tags));
                _MeterListener.Start();
            }

            if (captureTraces)
            {
                _ActivityListener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == CacheTelemetryNames.ActivitySourceName || source.Name == TestSourceName,
                    Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = activity =>
                    {
                        if (activity.Source.Name == CacheTelemetryNames.ActivitySourceName
                            && (activity.GetTagItem(CacheTelemetryNames.AttributeCacheName) as string) == _CacheName)
                        {
                            _Activities.Enqueue(activity);
                        }
                    }
                };
                ActivitySource.AddActivityListener(_ActivityListener);
            }
        }

        /// <summary>
        /// Name of an activity source tests may use to create ambient parent spans.
        /// </summary>
        public const string TestSourceName = "Caching.Tests";

        /// <summary>
        /// Captured measurements.
        /// </summary>
        public List<CapturedMeasurement> Measurements => _Measurements.ToList();

        /// <summary>
        /// Captured, stopped activities.
        /// </summary>
        public List<Activity> Activities => _Activities.ToList();

        /// <summary>
        /// Poll observable instruments (gauges) now.
        /// </summary>
        public void CollectGauges()
        {
            _MeterListener?.RecordObservableInstruments();
        }

        /// <summary>
        /// Measurements for one instrument, optionally filtered by tag values.
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="tagFilters">Alternating tag key and value pairs.</param>
        /// <returns>Matching measurements.</returns>
        public List<CapturedMeasurement> For(string instrument, params string[] tagFilters)
        {
            return Measurements.Where(m => m.Instrument == instrument && m.Matches(tagFilters)).ToList();
        }

        /// <summary>
        /// Sum of matching measurement values.
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="tagFilters">Alternating tag key and value pairs.</param>
        /// <returns>Sum.</returns>
        public double Sum(string instrument, params string[] tagFilters)
        {
            return For(instrument, tagFilters).Sum(m => m.Value);
        }

        /// <summary>
        /// Activities with the given display name.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <returns>Matching activities.</returns>
        public List<Activity> Spans(string name)
        {
            return Activities.Where(a => a.DisplayName == name).ToList();
        }

        /// <summary>
        /// Stop listening.
        /// </summary>
        public void Dispose()
        {
            _MeterListener?.Dispose();
            _ActivityListener?.Dispose();
        }

        private void OnMeasurement(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (KeyValuePair<string, object> tag in tags)
                dict[tag.Key] = tag.Value?.ToString();

            if (!dict.TryGetValue(CacheTelemetryNames.AttributeCacheName, out string name) || name != _CacheName) return;

            _Measurements.Enqueue(new CapturedMeasurement(instrument.Name, instrument.Unit, value, dict));
        }
    }
}
