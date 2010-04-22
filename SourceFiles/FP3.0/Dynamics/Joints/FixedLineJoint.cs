﻿/*
* Box2D.XNA port of Box2D:
* Copyright (c) 2009 Brandon Furtwangler, Nathan Furtwangler
*
* Original source Box2D:
* Copyright (c) 2006-2009 Erin Catto http://www.gphysics.com 
* 
* This software is provided 'as-is', without any express or implied 
* warranty.  In no event will the authors be held liable for any damages 
* arising from the use of this software. 
* Permission is granted to anyone to use this software for any purpose, 
* including commercial applications, and to alter it and redistribute it 
* freely, subject to the following restrictions: 
* 1. The origin of this software must not be misrepresented; you must not 
* claim that you wrote the original software. If you use this software 
* in a product, an acknowledgment in the product documentation would be 
* appreciated but is not required. 
* 2. Altered source versions must be plainly marked as such, and must not be 
* misrepresented as being the original software. 
* 3. This notice may not be removed or altered from any source distribution. 
*/

using System;
using FarseerPhysics.Common;
using Microsoft.Xna.Framework;

namespace FarseerPhysics.Dynamics.Joints
{
    /// <summary>
    /// A line joint. This joint provides two degrees of freedom: translation
    /// along an axis fixed in body1 and rotation in the plane. You can use a
    /// joint limit to restrict the range of motion and a joint motor to drive
    /// the motion or to model joint friction.
    /// </summary>
    public class FixedLineJoint : Joint
    {
        private Mat22 _K;
        private float _a1;
        private float _a2;
        private Vector2 _axis;
        private bool _enableLimit;
        private bool _enableMotor;
        private Vector2 _impulse;
        private LimitState _limitState;
        private Vector2 _localXAxis1;
        private Vector2 _localYAxis1;
        private float _lowerLimit;
        private float _maxMotorForce;
        private float _motorImpulse;
        private float _motorMass; // effective mass for motor/limit translational constraint.
        private float _motorSpeed;
        private Vector2 _perp;
        private float _s1;
        private float _s2;
        private float _upperLimit;

        /// <summary>
        /// Initialize the bodies, anchors, axis, and reference angle using the local
        /// anchor and world axis.
        /// This requires defining a line of
        /// motion using an axis and an anchor point. Uses local
        /// anchor points and a local axis so that the initial configuration
        /// can violate the constraint slightly. The joint translation is zero
        /// when the local anchor points coincide in world space. Using local
        /// anchors and a local axis helps when saving and loading a game.
        /// </summary>
        /// <param name="bodyA"></param>
        /// <param name="bodyB"></param>
        /// <param name="anchor"></param>
        /// <param name="axis"></param>
        public FixedLineJoint(Body bodyA, /*Body bodyB, */Vector2 anchor, Vector2 axis)
            : base(bodyA /*, bodyB*/)
        {
            JointType = JointType.FixedLine;

            BodyB = bodyA;

            LocalAnchorA = anchor; // BodyA.GetLocalPoint(anchor);
            LocalAnchorB = BodyB.GetLocalPoint(anchor);

            _localXAxis1 = bodyA.GetLocalVector(axis);
            _localYAxis1 = MathUtils.Cross(1.0f, _localXAxis1);
            _localYAxis1 = MathUtils.Cross(1.0f, _localXAxis1);

            _limitState = LimitState.Inactive;
        }

        public override Vector2 WorldAnchorA
        {
            get { return LocalAnchorA; } // BodyA.GetWorldPoint(LocalAnchorA); }
        }

        public override Vector2 WorldAnchorB
        {
            get { return BodyB.GetWorldPoint(LocalAnchorB); }
        }

        /// <summary>
        /// Enable/disable the joint limit.
        /// </summary>
        /// <value>if set to &lt;c&gt;true&lt;/c&gt; [flag].</value>
        public bool EnableLimit
        {
            set
            {
                WakeBodies();
                _enableLimit = value;
            }
            get { return _enableLimit; }
        }

        /// <summary>
        /// The lower translation limit, usually in meters.
        /// </summary>
        public float LowerLimit
        {
            get { return _lowerLimit; }
            set
            {
                WakeBodies();
                _lowerLimit = value;
            }
        }

        /// <summary>
        /// The upper translation limit, usually in meters.
        /// </summary>
        public float UpperLimit
        {
            get { return _upperLimit; }
            set
            {
                WakeBodies();
                _upperLimit = value;
            }
        }

        /// <summary>
        /// Enable/disable the joint motor.
        /// </summary>
        /// <value>
        ///   &lt;c&gt;true&lt;/c&gt; if [is motor enabled]; otherwise, &lt;c&gt;false&lt;/c&gt;.
        /// </value>
        public bool MotorEnabled
        {
            get { return _enableMotor; }
            set
            {
                WakeBodies();
                _enableMotor = value;
            }
        }

        /// <summary>
        /// The desired motor speed in radians per second.
        /// </summary>
        public float MotorSpeed
        {
            set
            {
                WakeBodies();
                _motorSpeed = value;
            }
            get { return _motorSpeed; }
        }

        /// <summary>
        /// The maximum motor torque, usually in N-m.
        /// </summary>
        public float MaxMotorForce
        {
            get { return _maxMotorForce; }
            set
            {
                WakeBodies();
                _maxMotorForce = value;
            }
        }

        /// <summary>
        /// Get the current motor force, usually in N.
        /// </summary>
        /// <value></value>
        public float MotorForce
        {
            get { return _motorImpulse; }
            set { _motorImpulse = value; }
        }

        /// <summary>
        /// The local anchor point relative to body1's origin.
        /// </summary>
        public Vector2 LocalAnchorA { get; set; }

        /// <summary>
        /// The local anchor point relative to body2's origin.
        /// </summary>
        public Vector2 LocalAnchorB { get; set; }

        /// <summary>
        /// Get the current joint translation, usually in meters.
        /// </summary>
        /// <value></value>
        public float JointTranslation
        {
            get
            {
                Vector2 d = BodyB.GetWorldPoint(LocalAnchorB) - LocalAnchorA; // BodyA.GetWorldPoint(LocalAnchorA);
                Vector2 axis = _localXAxis1; // BodyA.GetWorldVector(_localXAxis1);

                return Vector2.Dot(d, axis);
            }
        }

        /// <summary>
        /// Get the current joint translation speed, usually in meters per second.
        /// </summary>
        /// <value></value>
        public float JointSpeed
        {
            get
            {
                Transform /*xf1,*/ xf2;
                //BodyA.GetTransform(out xf1);
                BodyB.GetTransform(out xf2);

                Vector2 r1 = LocalAnchorA; // MathUtils.Multiply(ref xf1.R, LocalAnchorA - BodyA.LocalCenter);
                Vector2 r2 = MathUtils.Multiply(ref xf2.R, LocalAnchorB - BodyB.LocalCenter);
                Vector2 p1 = r1; // BodyA._sweep.Center + r1;
                Vector2 p2 = BodyB._sweep.Center + r2;
                Vector2 d = p2 - p1;
                Vector2 axis = _localXAxis1; // BodyA.GetWorldVector(_localXAxis1);

                Vector2 v1 = Vector2.Zero; // BodyA._linearVelocity;
                Vector2 v2 = BodyB._linearVelocity;
                float w1 = 0.0f; // BodyA._angularVelocity;
                float w2 = BodyB._angularVelocity;

                float speed = Vector2.Dot(d, MathUtils.Cross(w1, axis)) +
                              Vector2.Dot(axis, v2 + MathUtils.Cross(w2, r2) - v1 - MathUtils.Cross(w1, r1));
                return speed;
            }
        }

        public override Vector2 GetReactionForce(float inv_dt)
        {
            return inv_dt * (_impulse.X * _perp + (_motorImpulse + _impulse.Y) * _axis);
        }

        public override float GetReactionTorque(float inv_dt)
        {
            return 0.0f;
        }

        internal override void InitVelocityConstraints(ref TimeStep step)
        {
            //Body b1 = BodyA;
            Body b2 = BodyB;

            _localCenterA = Vector2.Zero; // b1.LocalCenter;
            _localCenterB = b2.LocalCenter;

            Transform /*xf1,*/ xf2;
            //b1.GetTransform(out xf1);
            b2.GetTransform(out xf2);

            // Compute the effective masses.
            Vector2 r1 = LocalAnchorA; // MathUtils.Multiply(ref xf1.R, LocalAnchorA - _localCenterA);
            Vector2 r2 = MathUtils.Multiply(ref xf2.R, LocalAnchorB - _localCenterB);
            Vector2 d = b2._sweep.Center + r2 /*- b1._sweep.Center*/- r1;

            _invMassA = 0.0f; // b1._invMass;
            _invIA = 0.0f; // b1._invI;
            _invMassB = b2._invMass;
            _invIB = b2._invI;

            // Compute motor Jacobian and effective mass.
            {
                _axis = _localXAxis1; // MathUtils.Multiply(ref xf1.R, _localXAxis1);
                _a1 = MathUtils.Cross(d + r1, _axis);
                _a2 = MathUtils.Cross(r2, _axis);

                _motorMass = _invMassA + _invMassB + _invIA * _a1 * _a1 + _invIB * _a2 * _a2;
                if (_motorMass > Settings.Epsilon)
                {
                    _motorMass = 1.0f / _motorMass;
                }
                else
                {
                    _motorMass = 0.0f;
                }
            }

            // Prismatic constraint.
            {
                _perp = _localYAxis1; // MathUtils.Multiply(ref xf1.R, _localYAxis1);

                _s1 = MathUtils.Cross(d + r1, _perp);
                _s2 = MathUtils.Cross(r2, _perp);

                float m1 = _invMassA, m2 = _invMassB;
                float i1 = _invIA, i2 = _invIB;

                float k11 = m1 + m2 + i1 * _s1 * _s1 + i2 * _s2 * _s2;
                float k12 = i1 * _s1 * _a1 + i2 * _s2 * _a2;
                float k22 = m1 + m2 + i1 * _a1 * _a1 + i2 * _a2 * _a2;

                _K.Col1 = new Vector2(k11, k12);
                _K.Col2 = new Vector2(k12, k22);
            }

            // Compute motor and limit terms.
            if (_enableLimit)
            {
                float jointTranslation = Vector2.Dot(_axis, d);
                if (Math.Abs(UpperLimit - LowerLimit) < 2.0f * Settings.LinearSlop)
                {
                    _limitState = LimitState.Equal;
                }
                else if (jointTranslation <= LowerLimit)
                {
                    if (_limitState != LimitState.AtLower)
                    {
                        _limitState = LimitState.AtLower;
                        _impulse.Y = 0.0f;
                    }
                }
                else if (jointTranslation >= UpperLimit)
                {
                    if (_limitState != LimitState.AtUpper)
                    {
                        _limitState = LimitState.AtUpper;
                        _impulse.Y = 0.0f;
                    }
                }
                else
                {
                    _limitState = LimitState.Inactive;
                    _impulse.Y = 0.0f;
                }
            }
            else
            {
                _limitState = LimitState.Inactive;
            }

            if (_enableMotor == false)
            {
                _motorImpulse = 0.0f;
            }

            if (step.WarmStarting)
            {
                // Account for variable time step.
                _impulse *= step.DtRatio;
                _motorImpulse *= step.DtRatio;

                Vector2 P = _impulse.X * _perp + (_motorImpulse + _impulse.Y) * _axis;
                float L1 = _impulse.X * _s1 + (_motorImpulse + _impulse.Y) * _a1;
                float L2 = _impulse.X * _s2 + (_motorImpulse + _impulse.Y) * _a2;

                //b1._linearVelocity -= _invMassA * P;
                //b1._angularVelocity -= _invIA * L1;

                b2._linearVelocity += _invMassB * P;
                b2._angularVelocity += _invIB * L2;
            }
            else
            {
                _impulse = Vector2.Zero;
                _motorImpulse = 0.0f;
            }
        }

        internal override void SolveVelocityConstraints(ref TimeStep step)
        {
            //Body b1 = BodyA;
            Body b2 = BodyB;

            Vector2 v1 = Vector2.Zero; // b1._linearVelocity;
            float w1 = 0.0f; // b1._angularVelocity;
            Vector2 v2 = b2._linearVelocity;
            float w2 = b2._angularVelocity;

            // Solve linear motor constraint.
            if (_enableMotor && _limitState != LimitState.Equal)
            {
                float Cdot = Vector2.Dot(_axis, v2 - v1) + _a2 * w2 - _a1 * w1;
                float impulse = _motorMass * (_motorSpeed - Cdot);
                float oldImpulse = _motorImpulse;
                float maxImpulse = step.DeltaTime * _maxMotorForce;
                _motorImpulse = MathUtils.Clamp(_motorImpulse + impulse, -maxImpulse, maxImpulse);
                impulse = _motorImpulse - oldImpulse;

                Vector2 P = impulse * _axis;
                float L1 = impulse * _a1;
                float L2 = impulse * _a2;

                v1 -= _invMassA * P;
                w1 -= _invIA * L1;

                v2 += _invMassB * P;
                w2 += _invIB * L2;
            }

            float Cdot1 = Vector2.Dot(_perp, v2 - v1) + _s2 * w2 - _s1 * w1;

            if (_enableLimit && _limitState != LimitState.Inactive)
            {
                // Solve prismatic and limit constraint in block form.
                float Cdot2 = Vector2.Dot(_axis, v2 - v1) + _a2 * w2 - _a1 * w1;
                Vector2 Cdot = new Vector2(Cdot1, Cdot2);

                Vector2 f1 = _impulse;
                Vector2 df = _K.Solve(-Cdot);
                _impulse += df;

                if (_limitState == LimitState.AtLower)
                {
                    _impulse.Y = Math.Max(_impulse.Y, 0.0f);
                }
                else if (_limitState == LimitState.AtUpper)
                {
                    _impulse.Y = Math.Min(_impulse.Y, 0.0f);
                }

                // f2(1) = invK(1,1) * (-Cdot(1) - K(1,2) * (f2(2) - f1(2))) + f1(1)
                float b = -Cdot1 - (_impulse.Y - f1.Y) * _K.Col2.X;

                float f2r;
                if (_K.Col1.X != 0.0f)
                {
                    f2r = b / _K.Col1.X + f1.X;
                }
                else
                {
                    f2r = f1.X;
                }

                _impulse.X = f2r;

                df = _impulse - f1;

                Vector2 P = df.X * _perp + df.Y * _axis;
                float L1 = df.X * _s1 + df.Y * _a1;
                float L2 = df.X * _s2 + df.Y * _a2;

                v1 -= _invMassA * P;
                w1 -= _invIA * L1;

                v2 += _invMassB * P;
                w2 += _invIB * L2;
            }
            else
            {
                // Limit is inactive, just solve the prismatic constraint in block form.

                float df;
                if (_K.Col1.X != 0.0f)
                {
                    df = -Cdot1 / _K.Col1.X;
                }
                else
                {
                    df = 0.0f;
                }

                _impulse.X += df;

                Vector2 P = df * _perp;
                float L1 = df * _s1;
                float L2 = df * _s2;

                v1 -= _invMassA * P;
                w1 -= _invIA * L1;

                v2 += _invMassB * P;
                w2 += _invIB * L2;
            }

            //b1._linearVelocity = v1;
            //b1._angularVelocity = w1;
            b2._linearVelocity = v2;
            b2._angularVelocity = w2;
        }

        internal override bool SolvePositionConstraints()
        {
            //Body b1 = BodyA;
            Body b2 = BodyB;

            Vector2 c1 = Vector2.Zero; // b1._sweep.Center;
            float a1 = 0.0f; // b1._sweep.Angle;

            Vector2 c2 = b2._sweep.Center;
            float a2 = b2._sweep.Angle;

            // Solve linear limit constraint.
            float linearError = 0.0f;
            bool active = false;
            float C2 = 0.0f;

            Mat22 R1 = new Mat22(a1);
            Mat22 R2 = new Mat22(a2);

            Vector2 r1 = MathUtils.Multiply(ref R1, LocalAnchorA - _localCenterA);
            Vector2 r2 = MathUtils.Multiply(ref R2, LocalAnchorB - _localCenterB);
            Vector2 d = c2 + r2 - c1 - r1;

            if (_enableLimit)
            {
                _axis = MathUtils.Multiply(ref R1, _localXAxis1);

                _a1 = MathUtils.Cross(d + r1, _axis);
                _a2 = MathUtils.Cross(r2, _axis);

                float translation = Vector2.Dot(_axis, d);
                if (Math.Abs(UpperLimit - LowerLimit) < 2.0f * Settings.LinearSlop)
                {
                    // Prevent large angular corrections
                    C2 = MathUtils.Clamp(translation, -Settings.MaxLinearCorrection, Settings.MaxLinearCorrection);
                    linearError = Math.Abs(translation);
                    active = true;
                }
                else if (translation <= LowerLimit)
                {
                    // Prevent large linear corrections and allow some slop.
                    C2 = MathUtils.Clamp(translation - LowerLimit + Settings.LinearSlop, -Settings.MaxLinearCorrection,
                                         0.0f);
                    linearError = LowerLimit - translation;
                    active = true;
                }
                else if (translation >= UpperLimit)
                {
                    // Prevent large linear corrections and allow some slop.
                    C2 = MathUtils.Clamp(translation - UpperLimit - Settings.LinearSlop, 0.0f,
                                         Settings.MaxLinearCorrection);
                    linearError = translation - UpperLimit;
                    active = true;
                }
            }

            _perp = MathUtils.Multiply(ref R1, _localYAxis1);

            _s1 = MathUtils.Cross(d + r1, _perp);
            _s2 = MathUtils.Cross(r2, _perp);

            Vector2 impulse;
            float C1;
            C1 = Vector2.Dot(_perp, d);

            linearError = Math.Max(linearError, Math.Abs(C1));
            const float angularError = 0.0f;

            if (active)
            {
                float m1 = _invMassA, m2 = _invMassB;
                float i1 = _invIA, i2 = _invIB;

                float k11 = m1 + m2 + i1 * _s1 * _s1 + i2 * _s2 * _s2;
                float k12 = i1 * _s1 * _a1 + i2 * _s2 * _a2;
                float k22 = m1 + m2 + i1 * _a1 * _a1 + i2 * _a2 * _a2;

                _K.Col1 = new Vector2(k11, k12);
                _K.Col2 = new Vector2(k12, k22);

                Vector2 C = new Vector2(-C1, -C2);

                impulse = _K.Solve(C); //note i inverted above
            }
            else
            {
                float m1 = _invMassA, m2 = _invMassB;
                float i1 = _invIA, i2 = _invIB;

                float k11 = m1 + m2 + i1 * _s1 * _s1 + i2 * _s2 * _s2;

                float impulse1;
                if (k11 != 0.0f)
                {
                    impulse1 = -C1 / k11;
                }
                else
                {
                    impulse1 = 0.0f;
                }

                impulse.X = impulse1;
                impulse.Y = 0.0f;
            }

            Vector2 P = impulse.X * _perp + impulse.Y * _axis;
            float L1 = impulse.X * _s1 + impulse.Y * _a1;
            float L2 = impulse.X * _s2 + impulse.Y * _a2;

            c1 -= _invMassA * P;
            a1 -= _invIA * L1;
            c2 += _invMassB * P;
            a2 += _invIB * L2;

            // TODO_ERIN remove need for this.
            //b1._sweep.Center = c1;
            //b1._sweep.Angle = a1;
            b2._sweep.Center = c2;
            b2._sweep.Angle = a2;
            //b1.SynchronizeTransform();
            b2.SynchronizeTransform();

            return linearError <= Settings.LinearSlop && angularError <= Settings.AngularSlop;
        }
    }
}