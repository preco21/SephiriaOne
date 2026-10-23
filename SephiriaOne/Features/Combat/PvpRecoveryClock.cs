using System;

namespace SephiriaOne
{
    // Pair-local fixed deadlines, observed only at eligible damage admission.
    // Missed periods collapse to one recovery; no timer jobs or stored bursts.
    internal struct PvpRecoveryClock
    {
        private bool armed;
        private double next, last, interval;
        private long revision;

        internal bool Due(double now, PvpRapidSettings settings, long settingsRevision)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            if (!settings.PeriodicReset) { armed = false; return false; }
            double seconds = settings.ResetScale;
            if (!armed || seconds != interval || revision != settingsRevision || now < last)
            {
                armed = true; interval = seconds; revision = settingsRevision; last = now; next = now + seconds;
                return false; // Live edits do not mint credit.
            }
            if (now == last) return false;
            last = now;
            if (now < next) return false;
            next += (Math.Floor((now - next) / interval) + 1d) * interval;
            // Fractional division can round just below a whole missed period.
            // Keep the next deadline strictly in the future without a catch-up loop.
            if (next <= now) next += interval;
            return true;
        }
    }
}
