using System;
using System.Collections.Generic;
using System.Linq;

namespace SIL.HermitCrab
{
	/// <summary>
	/// This class represents an affixal morphological rule. It supports many different types of affixation,
	/// such as prefixation, suffixation, infixation, circumfixation, simulfixation, reduplication,
	/// and truncation.
	/// </summary>
	public class AffixalMorphologicalRule : MorphologicalRule
	{
		/// <summary>
		/// This represents a morphological subrule.
		/// </summary>
		public class Subrule : Allomorph
		{
			AlphaVariables m_alphaVars;
			MorphologicalTransform m_transform;
			PhoneticPattern m_lhsTemp;

			MPRFeatureSet m_excludedMPRFeatures = null;
			MPRFeatureSet m_requiredMPRFeatures = null;
			MPRFeatureSet m_outputMPRFeatures = null;

			/// <summary>
			/// Initializes a new instance of the <see cref="Subrule"/> class.
			/// </summary>
			/// <param name="id">The id.</param>
			/// <param name="desc">The description.</param>
			/// <param name="morpher">The morpher.</param>
			/// <param name="lhs">The LHS.</param>
			/// <param name="rhs">The RHS.</param>
			/// <param name="alphaVars">The alpha variables.</param>
			/// <param name="redupMorphType">The full reduplication type.</param>
			public Subrule(string id, string desc, Morpher morpher, IEnumerable<PhoneticPattern> lhs,
				IEnumerable<MorphologicalOutput> rhs, AlphaVariables alphaVars, MorphologicalTransform.RedupMorphType redupMorphType)
				: base (id, desc, morpher)
			{
				m_alphaVars = alphaVars;

				m_transform = new MorphologicalTransform(lhs, rhs, redupMorphType);

				// the LHS template is generated by simply concatenating all of the
				// LHS partitions; it matches the entire word, so we check for both the
				// left and right margins.
				m_lhsTemp = new PhoneticPattern();
				m_lhsTemp.Add(new MarginContext(Direction.LEFT));
				int partition = 0;
				foreach (PhoneticPattern pat in lhs)
				{
					m_lhsTemp.AddPartition(pat, partition, m_transform.IsGreedy(partition));
					partition++;
				}
				m_lhsTemp.Add(new MarginContext(Direction.RIGHT));
			}

			/// <summary>
			/// Gets or sets the excluded MPR features.
			/// </summary>
			/// <value>The excluded MPR features.</value>
			public MPRFeatureSet ExcludedMPRFeatures
			{
				get
				{
					return m_excludedMPRFeatures;
				}

				set
				{
					m_excludedMPRFeatures = value;
				}
			}

			/// <summary>
			/// Gets or sets the required MPR features.
			/// </summary>
			/// <value>The required MPR features.</value>
			public MPRFeatureSet RequiredMPRFeatures
			{
				get
				{
					return m_requiredMPRFeatures;
				}

				set
				{
					m_requiredMPRFeatures = value;
				}
			}

			/// <summary>
			/// Gets or sets the output MPR features.
			/// </summary>
			/// <value>The output MPR features.</value>
			public MPRFeatureSet OutputMPRFeatures
			{
				get
				{
					return m_outputMPRFeatures;
				}

				set
				{
					m_outputMPRFeatures = value;
				}
			}

			/// <summary>
			/// Unapplies this subrule to the input word analysis.
			/// </summary>
			/// <param name="input">The input word analysis.</param>
			/// <param name="output">The output word analyses.</param>
			/// <returns><c>true</c> if the subrule was successfully unapplied, otherwise <c>false</c></returns>
			public bool Unapply(WordAnalysis input, out ICollection<WordAnalysis> output)
			{
				VariableValues instantiatedVars = new VariableValues(m_alphaVars);
				IList<Match> matches;
				m_transform.RHSTemplate.IsMatch(input.Shape.First, Direction.RIGHT, ModeType.ANALYSIS, instantiatedVars, out matches);

				List<WordAnalysis> outputList = new List<WordAnalysis>();
				output = outputList;
				foreach (Match match in matches)
				{
					PhoneticShape shape = UnapplyRHS(match);

					if (shape.Count > 2)
					{
						// check to see if this is a duplicate of another output analysis, this is not strictly necessary, but
						// it helps to reduce the search space
						bool add = true;
						for (int i = 0; i < output.Count; i++)
						{
							if (shape.Duplicates(outputList[i].Shape))
							{
								if (shape.Count > outputList[i].Shape.Count)
									// if this is a duplicate and it is longer, then use this analysis and remove the previous one
									outputList.RemoveAt(i);
								else
									// if it is shorter, then do not add it to the output list
									add = false;
								break;
							}
						}

						if (add)
						{
							WordAnalysis wa = input.Clone();
							wa.Shape = shape;
							output.Add(wa);
						}
					}
				}

				return outputList.Count > 0;
			}

			PhoneticShape UnapplyRHS(Match match)
			{
				PhoneticShape output = new PhoneticShape();
				output.Add(new Margin(Direction.LEFT));
				// iterate thru LHS partitions, copying the matching partition from the
				// input to the output
				for (int i = 0; i < m_transform.PartitionCount; i++)
					m_transform.Unapply(match, i, output);
				output.Add(new Margin(Direction.RIGHT));
				return output;
			}

			/// <summary>
			/// Applies this subrule to the specified word synthesis.
			/// </summary>
			/// <param name="input">The input word synthesis.</param>
			/// <param name="output">The output word synthesis.</param>
			/// <returns><c>true</c> if the subrule was successfully applied, otherwise <c>false</c></returns>
			public bool Apply(WordSynthesis input, out WordSynthesis output)
			{
				output = null;

				// check MPR features
				if ((m_requiredMPRFeatures != null && m_requiredMPRFeatures.Count > 0 && !m_requiredMPRFeatures.IsMatch(input.MPRFeatures))
					|| (m_excludedMPRFeatures != null && m_excludedMPRFeatures.Count > 0 && m_excludedMPRFeatures.IsMatch(input.MPRFeatures)))
					return false;

				VariableValues instantiatedVars = new VariableValues(m_alphaVars);
				IList<Match> matches;
				if (m_lhsTemp.IsMatch(input.Shape.First, Direction.RIGHT, ModeType.SYNTHESIS, instantiatedVars, out matches))
				{
					output = input.Clone();
					ApplyRHS(matches[0], input, output);

					if (m_outputMPRFeatures != null)
						output.MPRFeatures.AddOutput(m_outputMPRFeatures);
					return true;
				}

				return false;
			}


			void ApplyRHS(Match match, WordSynthesis input, WordSynthesis output)
			{
				output.Shape.Clear();
				output.Morphs.Clear();
				output.Shape.Add(new Margin(Direction.LEFT));
				foreach (MorphologicalOutput outputMember in m_transform.RHS)
					outputMember.Apply(match, input, output, this);
				output.Shape.Add(new Margin(Direction.RIGHT));
			}

			public override bool ConstraintsEqual(Allomorph other)
			{
				Subrule otherSubrule = (Subrule) other;

				if (m_requiredMPRFeatures == null)
				{
					if (otherSubrule.m_requiredMPRFeatures != null)
						return false;
				}
				else
				{
					if (!m_requiredMPRFeatures.Equals(otherSubrule.m_requiredMPRFeatures))
						return false;
				}

				if (m_excludedMPRFeatures == null)
				{
					if (otherSubrule.m_excludedMPRFeatures != null)
						return false;
				}
				else
				{
					if (!m_excludedMPRFeatures.Equals(otherSubrule.m_excludedMPRFeatures))
						return false;
				}

				return m_lhsTemp.Equals(otherSubrule.m_lhsTemp) && base.ConstraintsEqual(other);
			}
		}

		List<Subrule> m_subrules;

		HCObjectSet<PartOfSpeech> m_requiredPOSs = null;
		PartOfSpeech m_outPOS = null;
		int m_maxNumApps = 1;
		FeatureValues m_requiredHeadFeatures = null;
		FeatureValues m_requiredFootFeatures = null;
		FeatureValues m_outHeadFeatures = null;
		FeatureValues m_outFootFeatures = null;
		HCObjectSet<Feature> m_obligHeadFeatures = null;
		// TODO: add subcats

		/// <summary>
		/// Initializes a new instance of the <see cref="MorphologicalRule"/> class.
		/// </summary>
		/// <param name="id">The id.</param>
		/// <param name="desc">The description.</param>
		/// <param name="morpher">The morpher.</param>
		public AffixalMorphologicalRule(string id, string desc, Morpher morpher)
			: base(id, desc, morpher)
		{
			m_subrules = new List<Subrule>();
		}

		/// <summary>
		/// Gets the maximum number of allowable applications of this rule.
		/// </summary>
		/// <value>The maximum number of applications.</value>
		public int MaxNumApps
		{
			get
			{
				return m_maxNumApps;
			}

			set
			{
				m_maxNumApps = value;
			}
		}

		/// <summary>
		/// Gets or sets the required parts of speech.
		/// </summary>
		/// <value>The required parts of speech.</value>
		public IEnumerable<PartOfSpeech> RequiredPOSs
		{
			get
			{
				return m_requiredPOSs;
			}

			set
			{
				m_requiredPOSs = new HCObjectSet<PartOfSpeech>(value);
			}
		}

		/// <summary>
		/// Gets or sets the output part of speech.
		/// </summary>
		/// <value>The output part of speech.</value>
		public PartOfSpeech OutPOS
		{
			get
			{
				return m_outPOS;
			}

			set
			{
				m_outPOS = value;
			}
		}

		/// <summary>
		/// Gets or sets the required head features.
		/// </summary>
		/// <value>The required head features.</value>
		public FeatureValues RequiredHeadFeatures
		{
			get
			{
				return m_requiredHeadFeatures;
			}

			set
			{
				m_requiredHeadFeatures = value;
			}
		}

		/// <summary>
		/// Gets or sets the required foot features.
		/// </summary>
		/// <value>The required foot features.</value>
		public FeatureValues RequiredFootFeatures
		{
			get
			{
				return m_requiredFootFeatures;
			}

			set
			{
				m_requiredFootFeatures = value;
			}
		}

		/// <summary>
		/// Gets or sets the output head features.
		/// </summary>
		/// <value>The output head features.</value>
		public FeatureValues OutHeadFeatures
		{
			get
			{
				return m_outHeadFeatures;
			}

			set
			{
				m_outHeadFeatures = value;
			}
		}

		/// <summary>
		/// Gets or sets the output foot features.
		/// </summary>
		/// <value>The output foot features.</value>
		public FeatureValues OutFootFeatures
		{
			get
			{
				return m_outFootFeatures;
			}

			set
			{
				m_outFootFeatures = value;
			}
		}

		/// <summary>
		/// Gets or sets the obligatory head features.
		/// </summary>
		/// <value>The obligatory head features.</value>
		public IEnumerable<Feature> ObligatoryHeadFeatures
		{
			get
			{
				return m_obligHeadFeatures;
			}

			set
			{
				m_obligHeadFeatures = new HCObjectSet<Feature>(value);
			}
		}

		/// <summary>
		/// Gets the subrules.
		/// </summary>
		/// <value>The subrules.</value>
		public IEnumerable<Subrule> Subrules
		{
			get
			{
				return m_subrules;
			}
		}

		/// <summary>
		/// Gets the number of subrules.
		/// </summary>
		/// <value>The number of subrules.</value>
		public override int SubruleCount
		{
			get
			{
				return m_subrules.Count;
			}
		}

		/// <summary>
		/// Adds a subrule.
		/// </summary>
		/// <param name="sr">The subrule.</param>
		public void AddSubrule(Subrule sr)
		{
			sr.Morpheme = this;
			sr.Index = m_subrules.Count;
			m_subrules.Add(sr);
		}

		/// <summary>
		/// Performs any pre-processing required for unapplication of a word analysis. This must
		/// be called before <c>Unapply</c>. <c>Unapply</c> and <c>EndUnapplication</c> should only
		/// be called if this method returns <c>true</c>.
		/// </summary>
		/// <param name="input">The input word analysis.</param>
		/// <returns>
		/// 	<c>true</c> if the specified input is unapplicable, otherwise <c>false</c>.
		/// </returns>
		public override bool BeginUnapplication(WordAnalysis input)
		{
			return input.GetNumUnappliesForMorphologicalRule(this) < m_maxNumApps
				&& (m_outPOS == null || input.MatchPOS(m_outPOS));
		}

		/// <summary>
		/// Unapplies the specified subrule to the specified word analysis.
		/// </summary>
		/// <param name="input">The input word analysis.</param>
		/// <param name="srIndex">Index of the subrule.</param>
		/// <param name="output">All resulting word analyses.</param>
		/// <param name="selectTraceMorphs">list of ids to be used in a selective trace</param>
		/// <returns>
		/// 	<c>true</c> if the subrule was successfully unapplied, otherwise <c>false</c>
		/// </returns>
		public override bool Unapply(WordAnalysis input, int srIndex, TraceManager trace, string[] selectTraceMorphs, out ICollection<WordAnalysis> output)
		{
			if (UseThisRule(selectTraceMorphs) && m_subrules[srIndex].Unapply(input, out output))
			{
				foreach (WordAnalysis wa in output)
				{
					if (m_requiredPOSs != null && m_requiredPOSs.Count > 0)
					{
						foreach (PartOfSpeech pos in m_requiredPOSs)
							wa.AddPOS(pos);
					}
					else if (m_outPOS == null)
					{
						wa.UninstantiatePOS();
					}

					wa.MorphologicalRuleUnapplied(this);

					if (trace != null)
						trace.MorphologicalRuleUnapplied(this, input, wa, m_subrules[srIndex]);
				}
				return true;
			}

			output = null;
			return false;
		}

		bool UseThisRule(string[] selectTraceMorphs)
		{
			if (selectTraceMorphs != null)
			{
				if (!selectTraceMorphs.Contains(ID))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Performs any post-processing required after the unapplication of a word analysis. This must
		/// be called after a successful <c>BeginUnapplication</c> call and any <c>Unapply</c> calls.
		/// </summary>
		/// <param name="input">The input word analysis.</param>
		/// <param name="trace"></param>
		/// <param name="unapplied">if set to <c>true</c> if the input word analysis was successfully unapplied.</param>
		public override void EndUnapplication(WordAnalysis input, TraceManager trace, bool unapplied)
		{
			if (trace != null && !unapplied)
				trace.MorphologicalRuleNotUnapplied(this, input);
		}

		/// <summary>
		/// Determines whether this rule is applicable to the specified word synthesis.
		/// </summary>
		/// <param name="input">The input word synthesis.</param>
		/// <returns>
		/// 	<c>true</c> if the rule is applicable, otherwise <c>false</c>.
		/// </returns>
		public override bool IsApplicable(WordSynthesis input)
		{
			// TODO: check subcats.

			// check required parts of speech
			return input.NextRule == this && input.GetNumAppliesForMorphologicalRule(this) < m_maxNumApps
				&& (m_requiredPOSs == null || m_requiredPOSs.Count == 0 || m_requiredPOSs.Contains(input.POS));
		}

		/// <summary>
		/// Applies the rule to the specified word synthesis.
		/// </summary>
		/// <param name="input">The input word synthesis.</param>
		/// <param name="output">The output word syntheses.</param>
		/// <returns>
		/// 	<c>true</c> if the rule was successfully applied, otherwise <c>false</c>
		/// </returns>
		public override bool Apply(WordSynthesis input, TraceManager trace, out ICollection<WordSynthesis> output)
		{
			output = null;

			// these should probably be moved to IsApplicable, but we will leave it here for
			// now so we don't have to call it again to set the features for the output word
			// synthesis record

			// check head features
			FeatureValues headFeatures;
			if (!m_requiredHeadFeatures.UnifyDefaults(input.HeadFeatures, out headFeatures))
				return false;

			// check foot features
			FeatureValues footFeatures;
			if (!m_requiredFootFeatures.UnifyDefaults(input.FootFeatures, out footFeatures))
				return false;

			output = new List<WordSynthesis>();
			for (int i = 0; i < m_subrules.Count; i++)
			{
				WordSynthesis ws;
				if (m_subrules[i].Apply(input, out ws))
				{
					if (m_outPOS != null)
						ws.POS = m_outPOS;

					if (m_outHeadFeatures != null)
						ws.HeadFeatures = m_outHeadFeatures.Clone();

					ws.HeadFeatures.Add(headFeatures);

					if (m_outFootFeatures != null)
						ws.FootFeatures = m_outFootFeatures.Clone();

					ws.FootFeatures.Add(footFeatures);

					if (m_obligHeadFeatures != null)
					{
						foreach (Feature feature in m_obligHeadFeatures)
							ws.AddObligatoryHeadFeature(feature);
					}

					ws.MorphologicalRuleApplied(this);

					ws = CheckBlocking(ws, trace);

					if (trace != null)
						trace.MorphologicalRuleApplied(this, input, ws, m_subrules[i]);

					output.Add(ws);
					// return all word syntheses that match subrules that are constrained by environments,
					// HC violates the disjunctive property of allomorphs here because it cannot check the
					// environmental constraints until it has a surface form, we will enforce the disjunctive
					// property of allomorphs at that time

					// HC also checks for free fluctuation, if the next subrule has the same constraints, we
					// do not treat them as disjunctive
					if ((i != m_subrules.Count - 1 && !m_subrules[i].ConstraintsEqual(m_subrules[i + 1]))
						&& m_subrules[i].RequiredEnvironments == null && m_subrules[i].ExcludedEnvironments == null)
					{
						break;
					}
				}
			}

			if (trace != null && output.Count == 0)
				trace.MorphologicalRuleNotApplied(this, input);

			return output.Count > 0;
		}

		/// <summary>
		/// Applies the rule to the specified word synthesis. This method is used by affix templates.
		/// </summary>
		/// <param name="input">The input word synthesis.</param>
		/// <param name="origHeadFeatures">The original head features before template application.</param>
		/// <param name="output">The output word syntheses.</param>
		/// <returns>
		/// 	<c>true</c> if the rule was successfully applied, otherwise <c>false</c>
		/// </returns>
		public override bool ApplySlotAffix(WordSynthesis input, FeatureValues origHeadFeatures, TraceManager trace, out ICollection<WordSynthesis> output)
		{
			return Apply(input, trace, out output);
		}

		public override void Reset()
		{
			base.Reset();

			m_requiredPOSs = null;
			m_outPOS = null;
			m_maxNumApps = 1;
			m_requiredHeadFeatures = null;
			m_requiredFootFeatures = null;
			m_outHeadFeatures = null;
			m_outFootFeatures = null;
			m_obligHeadFeatures = null;
			m_subrules.Clear();
		}
	}
}
