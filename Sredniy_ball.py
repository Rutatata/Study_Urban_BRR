grades = [[5, 3, 3, 5, 4], [2, 2, 2, 3], [4, 5, 5, 2], [4, 4, 3], [5, 5, 5, 4, 5]]
a = grades[0]
b = grades[1]
j = grades[2]
k = grades[3]
s = grades[4]
srednee_a = sum(a[0:5]) / len(a[0:5])
srednee_b = sum(b[0:5]) / len(b[0:5])
srednee_j = sum(j[0:5]) / len(j[0:5])
srednee_k = sum(k[0:5]) / len(k[0:5])
srednee_s = sum(s[0:5]) / len(s[0:5])
srednee = srednee_a, srednee_b, srednee_j, srednee_k, srednee_s
students = {'Johnny', 'Bilbo', 'Steve', 'Khendrik', 'Aaron'}
sorted_students = sorted(students)
a1 = sorted_students[0]
b2 = sorted_students[1]
j3 = sorted_students[2]
k4 = sorted_students[3]
s5 = sorted_students[4]
sr_ozenka = {a1: a, b2: b,j3: j,k4: k,s5: s}
print(sr_ozenka)




