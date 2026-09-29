using System;
using System.Collections.Generic;

namespace Barometer_UWP.Helpers
{
    /// <summary>
    /// Небольшой планетарий-движок: экваториальные -> горизонтальные координаты,
    /// фаза Луны, восход/закат светила. Используется для «карты неба» и темы Auto.
    /// Все расчёты — собственные (NOAA-подобные формулы), без внешних зависимостей,
    /// совместимы с .NET Native / Windows 10 Mobile (15254).
    /// </summary>
    public static class SkyMap
    {
        private const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;

        // Юлианская дата эпохи J2000.0
        private const double J2000 = 2451545.0;

        #region Time helpers

        /// <summary>Юлианская дата из UTC DateTime.</summary>
        public static double ToJulianDay(DateTime utc)
        {
            // Корректная формула (Meeus, ch.7). Предыдущая версия ошибочно
            // использовала DayOfYear вместо месяца/дня и не учитывала год.
            int y = utc.Year;
            int m = utc.Month;
            double d = utc.Day + (utc.Hour + utc.Minute + utc.Second / 60.0) / 24.0;
            if (m <= 2) { y -= 1; m += 12; }
            int A = y / 100;
            int B = 2 - A + A / 4;
            return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + B - 1524.5;
        }

        /// <summary>Звёздное время Гринвича, градусы [0..360).</summary>
        public static double GreenwichMeanSiderealTime(double jd)
        {
            double T = (jd - J2000) / 36525.0;
            double gmst = 280.46061837
                        + 360.98564736629 * (jd - J2000)
                        + 0.000387933 * T * T
                        - T * T * T / 38710000.0;
            return Normalize360(gmst);
        }

        private static double Normalize360(double deg)
        {
            deg %= 360.0;
            if (deg < 0) deg += 360.0;
            return deg;
        }

        /// <summary>Обратное преобразование юлианской даты в DateTime (UTC).</summary>
        public static DateTime FromJulianDayApprox(double jd)
        {
            // Meeus, ch.7 (обратная формула)
            double z = Math.Floor(jd + 0.5);
            double f = jd + 0.5 - z;
            double a = z;
            if (z >= 2299161)
            {
                double alpha = Math.Floor((z - 1867216.25) / 36524.25);
                a = z + 1.0 + Math.Floor(alpha / 4.0) - alpha;
            }
            double b = a + 1524.0;
            double c = Math.Floor((b - 122.1) / 365.25);
            double d0 = Math.Floor(365.25 * c);
            double e0 = Math.Floor((b - d0) / 30.6001);
            int day = (int)(b - d0 - Math.Floor(30.6001 * e0) + f);
            int month = e0 < 14 ? (int)e0 - 1 : (int)e0 - 13;
            int year = month > 2 ? (int)c - 4716 : (int)c - 4715;
            double fracDay = (jd + 0.5) - Math.Floor(jd + 0.5);
            long seconds = (long)Math.Round(fracDay * 86400.0);
            if (seconds >= 86400) seconds -= 86400;
            try { return new DateTime(year, month, day).AddSeconds(seconds); }
            catch { return DateTime.UtcNow; }
        }

        #endregion

        #region Coordinate conversion

        /// <summary>
        /// Экваториальные координаты (RA в градусах, Dec в градусах) ->
        /// горизонтальные (азимут от севера по часовой, высота над горизонтом).
        /// </summary>
        public static HorizontalCoord EquatorialToHorizontal(double raDeg, double decDeg,
                                                             double latDeg, double lonDeg, DateTime utc)
        {
            double jd = ToJulianDay(utc);
            double lst = Normalize360(GreenwichMeanSiderealTime(jd) + lonDeg); // восточная долгота "+"
            double ha = Normalize360(lst - raDeg) * Deg2Rad;                    // часовое угол
            double lat = latDeg * Deg2Rad;
            double dec = decDeg * Deg2Rad;

            double sinAlt = Math.Sin(lat) * Math.Sin(dec) + Math.Cos(lat) * Math.Cos(dec) * Math.Cos(ha);
            sinAlt = Math.Max(-1.0, Math.Min(1.0, sinAlt));
            double alt = Math.Asin(sinAlt) * Rad2Deg;

            double cosAz = (Math.Sin(dec) - Math.Sin(lat) * sinAlt) / (Math.Cos(lat) * Math.Cos(alt * Deg2Rad) + 1e-12);
            cosAz = Math.Max(-1.0, Math.Min(1.0, cosAz));
            double az = Math.Acos(cosAz) * Rad2Deg;
            if (Math.Sin(ha) > 0) az = 360.0 - az; // южная полушара правила

            return new HorizontalCoord { Azimuth = az, Altitude = alt };
        }

        #endregion

        #region Sun

        /// <summary>Позиция Солнца (геоцентрические RA/Dec, градусы) на момент UTC.</summary>
        public static EquatorialCoord SunPosition(DateTime utc)
        {
            double n = ToJulianDay(utc) - J2000;
            double L = Normalize360(280.460 + 0.9856474 * n);          // средняя долгота
            double g = Normalize360(357.528 + 0.9856003 * n) * Deg2Rad; // средняя аномалия
            double lambda = (L + 1.915 * Math.Sin(g) + 0.020 * Math.Sin(2 * g)) * Deg2Rad;
            double eps = (23.439 - 0.0000004 * n) * Deg2Rad;           // наклон эклиптики

            double ra = Math.Atan2(Math.Cos(eps) * Math.Sin(lambda), Math.Cos(lambda)) * Rad2Deg;
            double dec = Math.Asin(Math.Sin(eps) * Math.Sin(lambda)) * Rad2Deg;
            return new EquatorialCoord { RaDeg = Normalize360(ra), DecDeg = dec };
        }

        /// <summary>
        /// Восход/закат Солнца (локальное civil-время). Возвращает null, если события нет
        /// (полярный день/ночь). Исправлено: раньше возвращались времена «на нулевом меридиане»,
        /// что давало ошибку до |longitude|/15 часов.
        /// </summary>
        public static SunTimes GetSunTimes(double latDeg, double lonDeg, DateTime localDate, double tzOffsetHours)
        {
            // NOAA zenith для восхода/заката: 90.833° (рефракция + полудиаметр)
            return ComputeRiseSet(latDeg, lonDeg, localDate, tzOffsetHours, isSun: true, zenithDeg: 90.833);
        }

        private static SunTimes ComputeRiseSet(double lat, double lon, DateTime date, double tz, bool isSun, double zenithDeg)
        {
            // Устойчивый метод: суточное сканирование высоты светила (шаг 1 мин) +
            // линейная интерполяция пересечения. Итеративный NOAA-корень оказался
            // неустойчивым (расходился на части суток), поэтому заменён в v1.4.0.3.
            double targetAlt = 90.0 - zenithDeg; // для Солнца: -0.833°
            var result = new SunTimes();
            double prevA = double.NaN; double prevH = 0;
            for (int m = 0; m <= 1440; m++)
            {
                double hh = m / 60.0;                 // локальный час
                double utcFrac = (hh - tz) / 24.0;    // UT-доля суток
                double jdDay = ToJulianDay(date.Date) + utcFrac;
                var eq = isSun ? SunPosition(FromJulianDayApprox(jdDay)) : MoonPosition(FromJulianDayApprox(jdDay));
                double lst = Normalize360(GreenwichMeanSiderealTime(jdDay) + lon);
                double H = Normalize360(lst - eq.RaDeg + 180) - 180;
                double alt = Math.Asin(Math.Sin(lat * Deg2Rad) * Math.Sin(eq.DecDeg * Deg2Rad)
                        + Math.Cos(lat * Deg2Rad) * Math.Cos(eq.DecDeg * Deg2Rad) * Math.Cos(H * Deg2Rad)) * Rad2Deg;
                double a = alt - targetAlt;
                if (!double.IsNaN(prevA) && prevA * a < 0)
                {
                    double f = prevA / (prevA - a);
                    double crossHh = prevH + f * (hh - prevH);
                    DateTime ev = date.Date.AddMinutes(Math.Round(crossHh * 60.0));
                    bool rising = a > 0;
                    if (rising && !result.Rise.HasValue) result.Rise = ev;
                    else if (!rising && result.Rise.HasValue && !result.Set.HasValue) result.Set = ev;
                }
                prevA = a; prevH = hh;
            }
            return result;
        }



        #endregion

        #region Moon

        /// <summary>Позиция Луны (упрощённая теория Meeus, точность ~0.3°, достаточно для карты).</summary>
        public static EquatorialCoord MoonPosition(DateTime utc)
        {
            double T = (ToJulianDay(utc) - J2000) / 36525.0;
            double L = Normalize360(218.3164477 + 481267.88123421 * T);   // средняя долгота
            double D = Normalize360(297.8501921 + 445267.1114034 * T);    // elongation
            double M = Normalize360(134.9633964 + 477198.8675055 * T);    // ср. аномалия Солнца
            double Mp = Normalize360(250.3447714 + 483202.0178763 * T);   // ср. аномалия Луны
            double F = Normalize360(304.2992739 + 481266.4843349 * T);    // аргумент широты

            double lonEcl = L
                + 6.289 * Math.Sin(Mp * Deg2Rad)
                + 1.274 * Math.Sin((2 * D - Mp) * Deg2Rad)
                + 0.658 * Math.Sin(2 * D * Deg2Rad)
                + 0.214 * Math.Sin(2 * Mp * Deg2Rad)
                - 0.186 * Math.Sin(M * Deg2Rad)
                - 0.114 * Math.Sin(2 * F * Deg2Rad);

            double latEcl = 5.128 * Math.Sin(F * Deg2Rad)
                          + 0.281 * Math.Sin((Mp + F) * Deg2Rad)
                          + 0.278 * Math.Sin((Mp - F) * Deg2Rad)
                          + 0.173 * Math.Sin((2 * D - F) * Deg2Rad);

            double eps = 23.4392911 * Deg2Rad;
            double lam = lonEcl * Deg2Rad, bet = latEcl * Deg2Rad;
            double ra = Math.Atan2(Math.Sin(lam) * Math.Cos(eps) - Math.Tan(bet) * Math.Sin(eps), Math.Cos(lam)) * Rad2Deg;
            double dec = Math.Asin(Math.Sin(bet) * Math.Cos(eps) + Math.Cos(bet) * Math.Sin(eps) * Math.Sin(lam)) * Rad2Deg;
            return new EquatorialCoord { RaDeg = Normalize360(ra), DecDeg = dec };
        }

        /// <summary>
        /// Фаза и освещённость Луны. BUGFIX v1.4.0.3: прежняя реализация ряда Meeus
        /// из-за путаницы градусов/радианов всегда давала illumination≈1.0.
        /// Теперь — надёжная модель по возрасту луны от эталонного новолуния.
        /// </summary>
        public static MoonPhaseInfo GetMoonPhase(DateTime utc)
        {
            // BUGFIX v1.4.0.3: предыдущая реализация ряда Meeus давала illumination≈1.0 всегда
            // (аргументы cos были в радианах, а коэффициенты — градусные поправки, и знак
            // освещённости был инвертирован). Надёжный метод: возраст луны от эталонного
            // новолуния + синусоидальная модель освещённости (точность ±0.02).
            DateTime knownNew = new DateTime(2000, 1, 6, 18, 14, 0, DateTimeKind.Utc);
            const double synodic = 29.530588853;
            var u = utc.Kind == DateTimeKind.Utc ? utc : utc.ToUniversalTime();
            double days = u.Subtract(knownNew).TotalDays;
            double age = days % synodic; if (age < 0) age += synodic;
            double phase = age / synodic;                     // 0=new, 0.5=full
            double illum = (1.0 - Math.Cos(2.0 * Math.PI * phase)) / 2.0;

            return new MoonPhaseInfo
            {
                Phase = phase,
                Illumination = illum,
                AgeDays = age
            };
        }

        #region Star catalog (ярчайшие звёзды, epoch J2000)

        public static readonly IReadOnlyList<Star> BrightStars = new List<Star>
        {
            new Star("Sirius",     101.287, -16.716, -1.46),
            new Star("Canopus",     95.988, -52.696, -0.74),
            new Star("Arcturus",    213.915,  19.182, -0.05),
            new Star("Vega",        279.234,  38.784,  0.03),
            new Star("Capella",     79.172,   45.998,  0.08),
            new Star("Rigel",       78.634,   -8.202,  0.13),
            new Star("Procyon",    114.825,    5.225,  0.34),
            new Star("Betelgeuse",  88.793,    7.407,  0.50),
            new Star("Achernar",    24.428,  -57.237,  0.46),
            new Star("Altair",     297.696,    8.868,  0.77),
            new Star("Aldebaran",    68.980,   16.509,  0.85),
            new Star("Antares",     247.352,  -26.432,  1.09),
            new Star("Spica",       201.298,  -11.161,  0.97),
            new Star("Pollux",      116.329,   28.026,  1.14),
            new Star("Fomalhaut",   344.413,  -29.622,  1.16),
            new Star("Deneb",       310.358,   45.280,  1.25),
            new Star("Regulus",     152.093,   11.967,  1.35),
            new Star("Castor",     113.650,   31.888,  1.58),
            new Star("Polaris",     37.954,   89.264,  1.98),
            new Star("Bellatrix",   81.283,    6.350,  1.64),
            new Star("Elnath",      81.628,   28.607,  1.65),
            new Star("Alnilam",     84.053,   -1.202,  1.69),
            new Star("Miaplacidus", 138.300,  -69.720,  1.69),
            new Star("Dubhe",      165.932,   61.751,  1.79),
            new Star("Mirfak",      51.081,   49.861,  1.79),
            new Star("Wezen",       107.098,  -26.393,  1.83),
            new Star("Sargas",     264.334,  -42.998,  1.86),
            new Star("Kaus Australis",276.043,-34.385,  1.85),
            new Star("Avior",      125.628,   -59.693,  1.86),
            new Star("Alkaid",       206.885,  49.313,  1.85),
            new Star("Menkalinan",   114.192,  44.445,  1.90),
            new Star("Atria",        169.620,  -69.028,  1.91),
            new Star("Alhena",        99.428,  16.399,  1.92),
            new Star("Peacock",       306.933, -56.736,  1.94),
            new Star("Mirzam",         95.250, -17.308,  1.98),
            new Star("Alphard",       141.897,  -8.659,  1.98),
        };

        #endregion

        /// <summary>Видимые объекты (высота > minAlt) для рендера карты неба.</summary>
        public static List<SkyObject> VisibleObjects(double lat, double lon, DateTime utc, double minAlt = 0.0)
        {
            var list = new List<SkyObject>();
            foreach (var s in BrightStars)
            {
                var h = EquatorialToHorizontal(s.RaDeg, s.DecDeg, lat, lon, utc);
                if (h.Altitude >= minAlt)
                    list.Add(new SkyObject { Name = s.Name, Mag = s.Magnitude, Az = h.Azimuth, Alt = h.Altitude, IsStar = true });
            }
            var sunEq = SunPosition(utc);
            var sunH = EquatorialToHorizontal(sunEq.RaDeg, sunEq.DecDeg, lat, lon, utc);
            list.Add(new SkyObject { Name = "Sun", Mag = -26.7, Az = sunH.Azimuth, Alt = sunH.Altitude, IsStar = false });
            var moonEq = MoonPosition(utc);
            var moonH = EquatorialToHorizontal(moonEq.RaDeg, moonEq.DecDeg, lat, lon, utc);
            list.Add(new SkyObject { Name = "Moon", Mag = -12.7, Az = moonH.Azimuth, Alt = moonH.Altitude, IsStar = false });
            return list;
        }

        public struct EquatorialCoord { public double RaDeg; public double DecDeg; }
        public struct HorizontalCoord { public double Azimuth; public double Altitude; }

        public sealed class Star
        {
            public string Name { get; }
            public double RaDeg { get; }
            public double DecDeg { get; }
            public double Magnitude { get; }
            public Star(string name, double ra, double dec, double mag)
            { Name = name; RaDeg = ra; DecDeg = dec; Magnitude = mag; }
        }

        public sealed class SkyObject
        {
            public string Name { get; set; }
            public double Mag { get; set; }
            public double Az { get; set; }
            public double Alt { get; set; }
            public bool IsStar { get; set; }
        }

        public sealed class SunTimes
        {
            public DateTime? Rise { get; internal set; }
            public DateTime? Set { get; internal set; }
        }

        public sealed class MoonPhaseInfo
        {
            public double Phase { get; set; }        // 0..1
            public double Illumination { get; set; } // 0..1
            public double AgeDays { get; set; }
        }
    }
}
