public partial class UnitAvatar
{
    public float moveSpeed = 4.4f;
    public int MovementWrites;
    public float NetworkmoveSpeed
    {
        get => moveSpeed;
        set { moveSpeed = value; MovementWrites++; }
    }
}
