using System;
using Vintagestory.API.Datastructures;

namespace Vintagestory.GameContent.Mechanics
{
    public sealed class LinkageKinematics
    {
        const int branchSamples = 360;
        const double distanceTolerance = 0.0000001;
        const double linearTolerance = 0.000001;
        const double derivativeStep = 0.0001;

        readonly double[] branchAngles;
        readonly double linearAngleOffset;
        readonly double signedDriveRadius;
        readonly double signedDrivenRadius;
        readonly double rodLengthSquared;
        readonly double radiusSquaredDifference;
        readonly double minPinDistance;
        readonly double maxPinDistance;

        public double CenterHorizontal { get; }
        public double CenterVertical { get; }
        public double RodLength { get; }
        public float DriveRadius { get; }
        public float DrivenRadius { get; }
        public float DrivePhase { get; }
        public float DrivenPhase { get; }
        public int RadialSign { get; }
        public bool IsLinear { get; }
        public bool IsIdentity { get; }
        public bool IsValid { get; }

        public LinkageKinematics(double centerHorizontal, double centerVertical, double rodLength, float driveRadius, float drivenRadius, float drivePhase, float drivenPhase, int radialSign)
        {
            CenterHorizontal = centerHorizontal;
            CenterVertical = centerVertical;
            RodLength = rodLength;
            DriveRadius = driveRadius;
            DrivenRadius = drivenRadius;
            DrivePhase = drivePhase;
            DrivenPhase = drivenPhase;
            RadialSign = radialSign;
            signedDriveRadius = radialSign * driveRadius;
            signedDrivenRadius = radialSign * drivenRadius;
            rodLengthSquared = rodLength * rodLength;
            radiusSquaredDifference = rodLengthSquared - drivenRadius * drivenRadius;
            minPinDistance = Math.Abs(rodLength - drivenRadius) - distanceTolerance;
            maxPinDistance = rodLength + drivenRadius + distanceTolerance;
            double centerDistance = Math.Sqrt(centerHorizontal * centerHorizontal + centerVertical * centerVertical);
            IsLinear = Math.Abs(driveRadius - drivenRadius) <= linearTolerance
                && Math.Abs(rodLength - centerDistance) <= linearTolerance;
            linearAngleOffset = drivePhase - drivenPhase;
            IsIdentity = IsLinear && Math.Abs(linearAngleOffset) <= linearTolerance;
            branchAngles = IsLinear ? Array.Empty<double>() : new double[branchSamples + 1];

            // Both circles must intersect throughout the turn, including between branch samples.
            double minDistance = Math.Abs(centerDistance - driveRadius);
            double maxDistance = centerDistance + driveRadius;
            IsValid = IsLinear || minDistance > distanceTolerance
                && minDistance >= minPinDistance
                && maxDistance <= maxPinDistance
                && buildBranch();
        }

        public double GetDrivenAngle(double driveAngle)
        {
            if (!IsValid) throw new InvalidOperationException("Cannot solve an invalid linkage configuration.");
            if (IsLinear) return driveAngle + linearAngleOffset;

            getCandidateAngles(driveAngle, out double first, out double second);
            double reference = getBranchReference(driveAngle);
            first = unwrapNear(first, reference);
            second = unwrapNear(second, reference);
            return Math.Abs(first - reference) <= Math.Abs(second - reference) ? first : second;
        }

        public double GetSpeedRatio(double driveAngle)
        {
            if (IsLinear) return 1;

            double drivenAngle = GetDrivenAngle(driveAngle);
            double driveCrankAngle = driveAngle + DrivePhase;
            double drivenCrankAngle = drivenAngle + DrivenPhase;
            double driveSin = Math.Sin(driveCrankAngle);
            double driveCos = Math.Cos(driveCrankAngle);
            double drivenSin = Math.Sin(drivenCrankAngle);
            double drivenCos = Math.Cos(drivenCrankAngle);
            double rx = CenterHorizontal + signedDrivenRadius * drivenSin - signedDriveRadius * driveSin;
            double ry = CenterVertical - DrivenRadius * drivenCos + DriveRadius * driveCos;
            double driveDx = signedDriveRadius * driveCos;
            double driveDy = DriveRadius * driveSin;
            double drivenDx = signedDrivenRadius * drivenCos;
            double drivenDy = DrivenRadius * drivenSin;
            double divisor = rx * drivenDx + ry * drivenDy;
            if (Math.Abs(divisor) > distanceTolerance)
            {
                return (rx * driveDx + ry * driveDy) / divisor;
            }

            double before = unwrapNear(GetDrivenAngle(driveAngle - derivativeStep), drivenAngle);
            double after = unwrapNear(GetDrivenAngle(driveAngle + derivativeStep), drivenAngle);
            return (after - before) / (2 * derivativeStep);
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetDouble("centerHorizontal", CenterHorizontal);
            tree.SetDouble("centerVertical", CenterVertical);
            tree.SetDouble("rodLength", RodLength);
            tree.SetFloat("driveRadius", DriveRadius);
            tree.SetFloat("drivenRadius", DrivenRadius);
            tree.SetFloat("drivePhase", DrivePhase);
            tree.SetFloat("drivenPhase", DrivenPhase);
            tree.SetInt("radialSign", RadialSign);
        }

        public static LinkageKinematics FromTreeAttributes(ITreeAttribute tree)
        {
            return new(
                tree.GetDouble("centerHorizontal"),
                tree.GetDouble("centerVertical"),
                tree.GetDouble("rodLength"),
                tree.GetFloat("driveRadius"),
                tree.GetFloat("drivenRadius"),
                tree.GetFloat("drivePhase"),
                tree.GetFloat("drivenPhase"),
                tree.GetInt("radialSign")
            );
        }

        bool buildBranch()
        {
            double previous = 0;
            for (int i = 0; i <= branchSamples; i++)
            {
                double driveAngle = i * Math.Tau / branchSamples;
                if (!getCandidateAngles(driveAngle, out double first, out double second)) return false;

                first = unwrapNear(first, previous);
                second = unwrapNear(second, previous);
                previous = branchAngles[i] = Math.Abs(first - previous) <= Math.Abs(second - previous) ? first : second;
            }

            return true;
        }

        bool getCandidateAngles(double driveAngle, out double first, out double second)
        {
            double driveCrankAngle = driveAngle + DrivePhase;
            double px = signedDriveRadius * Math.Sin(driveCrankAngle);
            double py = -DriveRadius * Math.Cos(driveCrankAngle);
            double dx = CenterHorizontal - px;
            double dy = CenterVertical - py;
            double centerDistance = Math.Sqrt(dx * dx + dy * dy);
            if (centerDistance <= distanceTolerance
                || centerDistance > maxPinDistance
                || centerDistance < minPinDistance)
            {
                first = second = 0;
                return false;
            }

            double along = (radiusSquaredDifference + centerDistance * centerDistance) / (2 * centerDistance);
            double heightSquared = rodLengthSquared - along * along;
            if (heightSquared < -distanceTolerance)
            {
                first = second = 0;
                return false;
            }

            double height = Math.Sqrt(Math.Max(0, heightSquared));
            double baseX = px + along * dx / centerDistance;
            double baseY = py + along * dy / centerDistance;
            double offsetX = -dy * height / centerDistance;
            double offsetY = dx * height / centerDistance;
            first = getCrankAngle(baseX + offsetX, baseY + offsetY);
            second = getCrankAngle(baseX - offsetX, baseY - offsetY);
            return true;
        }

        double getCrankAngle(double pinX, double pinY)
        {
            return Math.Atan2(RadialSign * (pinX - CenterHorizontal), -(pinY - CenterVertical)) - DrivenPhase;
        }

        double getBranchReference(double driveAngle)
        {
            double cycle = Math.Floor(driveAngle / Math.Tau);
            double withinCycle = driveAngle - cycle * Math.Tau;
            double sample = withinCycle * branchSamples / Math.Tau;
            int lower = Math.Min((int)sample, branchSamples - 1);
            double fraction = sample - lower;
            double reference = branchAngles[lower] + (branchAngles[lower + 1] - branchAngles[lower]) * fraction;
            return reference + cycle * (branchAngles[branchSamples] - branchAngles[0]);
        }

        static double unwrapNear(double angle, double reference)
        {
            return angle + Math.Round((reference - angle) / Math.Tau) * Math.Tau;
        }
    }

    public sealed class LinkageAngleTransform(LinkageAngleTransform? inputTransform, LinkageKinematics kinematics, float inputRatio, bool inputReversed, float outputRatio, bool outputReversed)
    {
        readonly LinkageAngleTransform? inputTransform = inputTransform;
        readonly LinkageKinematics kinematics = kinematics;
        readonly float inputRatio = inputRatio;
        readonly float outputRatio = outputRatio;
        readonly bool inputReversed = inputReversed;
        readonly bool outputReversed = outputReversed;

        public int Depth { get; } = (inputTransform?.Depth ?? 0) + 1;

        public double GetAngle(double networkAngle, float nodeRatio, bool nodeReversed)
        {
            double outputAngle = kinematics.GetDrivenAngle(getInputAngle(networkAngle));
            double outputDerivative = getSignedRatio(outputRatio, outputReversed);
            return outputAngle * getSignedRatio(nodeRatio, nodeReversed) / outputDerivative;
        }

        public float GetSpeedFactor(double networkAngle)
        {
            double inputAngle = getInputAngle(networkAngle);
            double inputFactor = inputTransform?.GetSpeedFactor(networkAngle) ?? 1;
            double inputDerivative = getSignedRatio(inputRatio, inputReversed) * inputFactor;
            double outputDerivative = kinematics.GetSpeedRatio(inputAngle) * inputDerivative;
            return (float)(outputDerivative / getSignedRatio(outputRatio, outputReversed));
        }

        public static bool Matches(LinkageAngleTransform? first, LinkageAngleTransform? second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first == null || second == null) return false;

            LinkageKinematics firstLink = first.kinematics;
            LinkageKinematics secondLink = second.kinematics;
            return first.inputRatio == second.inputRatio
                && first.outputRatio == second.outputRatio
                && first.inputReversed == second.inputReversed
                && first.outputReversed == second.outputReversed
                && firstLink.CenterHorizontal == secondLink.CenterHorizontal
                && firstLink.CenterVertical == secondLink.CenterVertical
                && firstLink.RodLength == secondLink.RodLength
                && firstLink.DriveRadius == secondLink.DriveRadius
                && firstLink.DrivenRadius == secondLink.DrivenRadius
                && firstLink.DrivePhase == secondLink.DrivePhase
                && firstLink.DrivenPhase == secondLink.DrivenPhase
                && firstLink.RadialSign == secondLink.RadialSign
                && Matches(first.inputTransform, second.inputTransform);
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetFloat("inputRatio", inputRatio);
            tree.SetFloat("outputRatio", outputRatio);
            tree.SetBool("inputReversed", inputReversed);
            tree.SetBool("outputReversed", outputReversed);
            ITreeAttribute kinematicsTree = new TreeAttribute();
            kinematics.ToTreeAttributes(kinematicsTree);
            tree["kinematics"] = kinematicsTree;
            if (inputTransform == null) return;

            ITreeAttribute inputTree = new TreeAttribute();
            inputTransform.ToTreeAttributes(inputTree);
            tree["input"] = inputTree;
        }

        public static LinkageAngleTransform? FromTreeAttributes(ITreeAttribute? tree)
        {
            ITreeAttribute? kinematicsTree = tree?.GetTreeAttribute("kinematics");
            if (tree == null || kinematicsTree == null) return null;

            return new(
                FromTreeAttributes(tree.GetTreeAttribute("input")),
                LinkageKinematics.FromTreeAttributes(kinematicsTree),
                tree.GetFloat("inputRatio"),
                tree.GetBool("inputReversed"),
                tree.GetFloat("outputRatio"),
                tree.GetBool("outputReversed")
            );
        }

        double getInputAngle(double networkAngle)
        {
            return inputTransform?.GetAngle(networkAngle, inputRatio, inputReversed)
                ?? networkAngle * getSignedRatio(inputRatio, inputReversed);
        }

        static double getSignedRatio(float ratio, bool reversed)
        {
            return reversed ? -ratio : ratio;
        }
    }
}
