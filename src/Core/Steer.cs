namespace Mikro;

/// <summary>
/// Napęd pływaka. Przy liczbie Reynoldsa ~10⁻² samo ciało nie ma bezwładności (zatrzymuje się w ułamku µm),
/// ale aparat ruchu – rzęski lub wici – zmienia częstość i kierunek bicia w ciągu dziesiątych części sekundy.
/// Dlatego prędkość narasta, a obrót przyspiesza i hamuje płynnie; ciało wygina się wzdłuż toru skrętu.
/// </summary>
public struct Steer
{
    public float V, Omega;

    /// <param name="accel">szybkość zmiany ciągu (1/s czasu biologicznego)</param>
    /// <param name="maxW">maksymalna prędkość obrotu (rad/s czasu biologicznego)</param>
    public void Update(Ent e, float dt, float heading, float speed, float maxW, float accel = 3.5f, float turnAcc = 4)
    {
        V += (speed - V) * Damp(accel, dt);
        float err = WrapA(heading - e.A);
        float wT = Clamp(err * 2.5f, -maxW, maxW);
        Omega += (wT - Omega) * Damp(turnAcc, dt);
        e.A += Omega * dt;
        e.Vx = MathF.Cos(e.A) * V; e.Vy = MathF.Sin(e.A) * V;
    }

    /// <summary>Opór przy pchaniu przeszkody: im cięższa (albo nieruchoma) i im bardziej "na wprost", tym mocniej hamuje.</summary>
    public void Push(Ent me, Ent o, float nx, float ny)
    {
        float front = nx * MathF.Cos(me.A) + ny * MathF.Sin(me.A);
        if (front <= 0.15f || V <= 0) return;
        float heavy = o.Fixed ? 1 : o.Mass / (o.Mass + me.Mass);
        V *= 1 - 0.12f * heavy * front;
    }

    /// <summary>Wygięcie osi ciała (krzywizna toru / 2), ograniczone tak, by końce odchylały się najwyżej o frac szerokości.</summary>
    public float Bend(float length, float width, float refSpeed, float frac = 0.22f)
    {
        float k = Omega / Math.Max(MathF.Abs(V), refSpeed * 0.35f) * 0.5f;
        float max = frac * width / Math.Max(1e-3f, Sq(length / 2));
        return Clamp(k, -max, max);
    }
}
