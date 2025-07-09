import csv

def read_dict_from_csv(file_path, encoding='utf-8'):
    dictionary = {}
    with open(file_path, mode='r', encoding=encoding) as file:
        reader = csv.reader(file)
        for row in reader:
            if len(row) >= 2:  # Ensure the row has at least 2 columns
                try:
                    word = row[0]
                    value = int(row[1])
                    dictionary[word] = value
                except ValueError:
                    # Handle rows where conversion to int fails
                    print(f"Skipping row: {row}")
    return dictionary

# Read dictionaries from CSV files
dict1 = read_dict_from_csv('DataUKUS2016-2019.csv')
dict2 = read_dict_from_csv('2024DataUKUS2016-2019.csv')

# Compare values for each word in the two dictionaries and calculate percentage difference
comparison_results = {}
for word in dict1.keys():
    if word in dict2:
        value1 = dict1[word]
        value2 = dict2[word]
        difference = value1 - value2
        percentage_difference = (difference / value1) * 100 if value1 != 0 else 0
        comparison_results[word] = {
            "dict1": value1,
            "dict2": value2,
            "difference": difference,
            "percentage_difference": percentage_difference
        }

i = 0

# Print comparison results
for word, comparison in comparison_results.items():
    if abs(comparison['percentage_difference']) > 10 and comparison['dict1'] > 1500:
        i += 1
        # print(f"Word: {word}, Dict1: {comparison['dict1']}, Dict2: {comparison['dict2']}, "
            # f"Difference: {comparison['difference']}, Percentage Difference: {comparison['percentage_difference']:.2f}%")

print(i)